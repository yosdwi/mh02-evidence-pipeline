package status

import (
	"context"
	"encoding/json"
	"fmt"
	"log"
	"os"
	"sync"
	"time"

	"evidence-uploader/config"
)

type Reporter struct {
	config   *config.Config
	mutex    sync.RWMutex
	stopCh   chan struct{}
}

type StatusData struct {
	LastUpdate       string `json:"last_update"`
	Status          string `json:"status"`
	DeviceID        string `json:"device_id"`
	CompressionWorker string `json:"compression_worker"`
	SyncWorker      string `json:"sync_worker"`
	UploaderType    string `json:"uploader_type"`
	TotalSyncs      int    `json:"total_syncs,omitempty"`
	SuccessfulSyncs int    `json:"successful_syncs,omitempty"`
	FailedSyncs     int    `json:"failed_syncs,omitempty"`
	LastSyncTime    string `json:"last_sync_time,omitempty"`
	LastError       string `json:"last_error,omitempty"`
}

func NewReporter(cfg *config.Config) *Reporter {
	return &Reporter{
		config: cfg,
		stopCh: make(chan struct{}),
	}
}

func (r *Reporter) Start(ctx context.Context, worker interface{}) error {
	log.Println("Starting status reporter")

	ticker := time.NewTicker(r.config.Sync.StatusInterval)
	defer ticker.Stop()

	// Initial status update
	r.updateStatus(worker, true)

	for {
		select {
		case <-ctx.Done():
			// Final status update before shutdown
			r.updateStatus(worker, false)
			log.Println("Status reporter stopped by context")
			return ctx.Err()
		case <-r.stopCh:
			r.updateStatus(worker, false)
			log.Println("Status reporter stopped by signal")
			return nil
		case <-ticker.C:
			r.updateStatus(worker, true)
		}
	}
}

func (r *Reporter) Stop() {
	close(r.stopCh)
}

func (r *Reporter) updateStatus(worker interface{}, isActive bool) {
	r.mutex.Lock()
	defer r.mutex.Unlock()

	syncWorkerStatus := "INACTIVE"
	if isActive {
		syncWorkerStatus = "ACTIVE"
	}

	statusData := StatusData{
		LastUpdate:        time.Now().Format("2006-01-02T15:04:05.000Z"),
		Status:           "ONLINE",
		DeviceID:         r.config.DeviceID,
		CompressionWorker: "INACTIVE", // Not implemented in Go version
		SyncWorker:       syncWorkerStatus,
		UploaderType:     "RCLONE-GO",
	}

	// Try to get stats if worker supports it
	type StatsProvider interface {
		GetStats() *SyncStats
	}

	if sp, ok := worker.(StatsProvider); ok {
		stats := sp.GetStats()
		statusData.TotalSyncs = stats.TotalSyncs
		statusData.SuccessfulSyncs = stats.SuccessfulSyncs
		statusData.FailedSyncs = stats.FailedSyncs

		// Add last sync time if available
		if !stats.LastSyncTime.IsZero() {
			statusData.LastSyncTime = stats.LastSyncTime.Format("2006-01-02T15:04:05.000Z")
		}

		// Add last error if available
		if stats.LastError != "" {
			statusData.LastError = stats.LastError
		}
	}

	if err := r.writeStatusFile(statusData); err != nil {
		log.Printf("Failed to update status file: %v", err)
	}
}

func (r *Reporter) writeStatusFile(statusData StatusData) error {
	// Create temporary file first for atomic write
	tempFile := r.config.Paths.StatusFile + ".tmp"

	file, err := os.Create(tempFile)
	if err != nil {
		return fmt.Errorf("failed to create temp status file: %w", err)
	}

	encoder := json.NewEncoder(file)
	encoder.SetIndent("", "  ")

	if err := encoder.Encode(statusData); err != nil {
		file.Close()
		os.Remove(tempFile)
		return fmt.Errorf("failed to encode status JSON: %w", err)
	}

	file.Close()

	// Atomic rename
	if err := os.Rename(tempFile, r.config.Paths.StatusFile); err != nil {
		os.Remove(tempFile)
		return fmt.Errorf("failed to rename status file: %w", err)
	}

	return nil
}