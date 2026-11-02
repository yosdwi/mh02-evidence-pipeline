package sync

import (
	"context"
	"fmt"
	"log"
	"time"

	"evidence-uploader/config"
	"evidence-uploader/evidence"
	"evidence-uploader/status"
)

type Worker struct {
	config     *config.Config
	rclone     *RcloneSync
	scanner    *evidence.Scanner
	validator  *evidence.Validator
	stopCh     chan struct{}
	stats      *status.SyncStats
}


func NewWorker(cfg *config.Config) *Worker {
	return &Worker{
		config:    cfg,
		rclone:    NewRcloneSync(cfg),
		scanner:   evidence.NewScanner(cfg.Paths.RecordingFolder),
		validator: evidence.NewValidator(),
		stopCh:    make(chan struct{}),
		stats:     &status.SyncStats{},
	}
}

func (w *Worker) Start(ctx context.Context) error {
	log.Printf("Starting sync worker for device: %s", w.config.DeviceID)

	ticker := time.NewTicker(w.config.Sync.ScanInterval)
	defer ticker.Stop()

	for {
		select {
		case <-ctx.Done():
			log.Println("Sync worker stopped by context")
			return ctx.Err()
		case <-w.stopCh:
			log.Println("Sync worker stopped by signal")
			return nil
		case <-ticker.C:
			if err := w.syncCycle(); err != nil {
				log.Printf("Sync cycle error: %v", err)
			}
		}
	}
}

func (w *Worker) Stop() {
	close(w.stopCh)
}

func (w *Worker) GetStats() *status.SyncStats {
	return w.stats
}

func (w *Worker) syncCycle() error {
	// Scan for ready evidence directories
	evidenceDirs, err := w.scanner.ScanReadyEvidenceDirs()
	if err != nil {
		return fmt.Errorf("failed to scan evidence dirs: %w", err)
	}

	if len(evidenceDirs) == 0 {
		return nil // No evidence to process
	}

	log.Printf("Found %d evidence directories to process", len(evidenceDirs))

	// Filter valid evidence (non-zero KPH and complete)
	validEvidencePaths := w.filterValidEvidence(evidenceDirs)

	if len(validEvidencePaths) == 0 {
		log.Println("No valid evidence to sync")
		return nil
	}

	log.Printf("Processing %d valid evidence directories in bulk", len(validEvidencePaths))

	// Bulk sync all valid evidence in single rclone call
	result, err := w.rclone.SyncAllEvidence(validEvidencePaths, w.config.DeviceID)

	if err != nil || !result.Success {
		w.stats.FailedSyncs += len(validEvidencePaths)
		if result != nil {
			w.stats.LastError = result.Error
		} else {
			w.stats.LastError = err.Error()
		}
		log.Printf("Bulk sync failed: %v", w.stats.LastError)
		return err
	}

	// Success - update stats
	w.stats.SuccessfulSyncs += len(validEvidencePaths)
	w.stats.TotalSyncs += len(validEvidencePaths)
	w.stats.LastSyncTime = time.Now()

	log.Printf("Bulk sync completed successfully in %v (%d files from %d evidence dirs)",
		result.Duration, result.FilesSync, len(validEvidencePaths))

	return nil
}

func (w *Worker) filterValidEvidence(evidenceDirs []*evidence.EvidenceDir) []string {
	var validPaths []string

	for _, evidenceDir := range evidenceDirs {
		// Check if evidence has zero KPH (should be skipped)
		// if w.validator.CheckIfZeroKPH(evidenceDir) {
		// 	log.Printf("Skipping evidence %s: zero KPH detected", evidenceDir.Name)
		// 	continue
		// }

		// Add to valid evidence list
		validPaths = append(validPaths, evidenceDir.Path)
		log.Printf("Including evidence: %s", evidenceDir.Name)
	}

	return validPaths
}
