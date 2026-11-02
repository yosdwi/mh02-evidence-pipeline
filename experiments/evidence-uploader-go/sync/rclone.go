package sync

import (
	"context"
	"encoding/json"
	"fmt"
	"io/ioutil"
	"os/exec"
	"path/filepath"
	"strings"
	"time"

	"evidence-uploader/config"
)

type RcloneSync struct {
	config *config.Config
}

func NewRcloneSync(cfg *config.Config) *RcloneSync {
	return &RcloneSync{
		config: cfg,
	}
}

type SyncResult struct {
	Success   bool
	Error     string
	Duration  time.Duration
	FilesSync int
}

// SyncAllEvidence performs bulk sync of all valid evidence directories
func (r *RcloneSync) SyncAllEvidence(validEvidenceDirs []string, deviceID string) (*SyncResult, error) {
	start := time.Now()

	ctx, cancel := context.WithTimeout(context.Background(), r.config.Sync.Timeout)
	defer cancel()

	// Get absolute path for rclone config
	configPath, err := filepath.Abs(r.config.Paths.RcloneConfig)
	if err != nil {
		return &SyncResult{
			Success:  false,
			Error:    fmt.Sprintf("failed to resolve config path: %v", err),
			Duration: time.Since(start),
		}, err
	}

	// Create include pattern for valid evidence directories
	var includeFilters []string
	sentStart := time.Now().Unix()

	for _, evidenceDir := range validEvidenceDirs {
		evidenceName := filepath.Base(evidenceDir)
		// Include all required files from valid evidence directories
		includeFilters = append(includeFilters,
			"--filter", fmt.Sprintf("+ %s/*.mp4", evidenceName),
			"--filter", fmt.Sprintf("+ %s/*.json", evidenceName),
			"--filter", fmt.Sprintf("+ %s/*.jpg", evidenceName),
			"--filter", fmt.Sprintf("+ %s/*.txt", evidenceName),
		)
	}


	// Build rclone command for bulk sync
	args := []string{
		"copy",
		r.config.Paths.RecordingFolder, // Sync from recording root
		fmt.Sprintf("minio:%s/%s/", r.config.MinIO.Bucket, deviceID),
		"--config", configPath,
		"--filter", "- *testing*/**",
		"--filter", "- *TESTING*/**",
		"--filter", "- /[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]/**",
	}

	// Add dynamic include filters for valid evidence
	args = append(args, includeFilters...)

	// Update each JSON file with sent_start timestamp before upload
	err = r.updateEvidenceJSONFiles(validEvidenceDirs, sentStart, 0) // Start with retry count 0
	if err != nil {
		return &SyncResult{
			Success:  false,
			Error:    fmt.Sprintf("failed to update JSON files with sent_start: %v", err),
			Duration: time.Since(start),
		}, err
	}

	// Exclude everything else
	args = append(args, []string{
		"--filter", "- *",
		"--timeout", fmt.Sprintf("%.0fs", r.config.Sync.Timeout.Seconds()-10),
		"--retries", fmt.Sprintf("%d", r.config.Sync.MaxRetries),
		"--transfers", fmt.Sprintf("%d", r.config.Sync.MaxTransfers),
		"--checkers", "6",
		"--buffer-size", "64M",
		"--use-mmap",
		"--log-level", "INFO",
		"--stats", "5s", // Progress every 5s
		"--stats-one-line", // Compact progress
	}...)

	cmd := exec.CommandContext(ctx, r.config.Paths.RcloneExe, args...)

	// Capture output for debugging
	output, err := cmd.CombinedOutput()
	duration := time.Since(start)

	result := &SyncResult{
		Duration: duration,
	}

	if err != nil {
		result.Success = false
		result.Error = fmt.Sprintf("rclone failed: %v\nOutput: %s", err, string(output))

		// Check if it was a timeout
		if ctx.Err() == context.DeadlineExceeded {
			result.Error = fmt.Sprintf("sync timeout after %v", duration)
		}

		// No cleanup needed for tags approach

		return result, err
	}

	result.Success = true
	// Parse output for file count if needed (optional)
	result.FilesSync = r.parseFilesTransferred(string(output))

	return result, nil
}

func (r *RcloneSync) parseFilesTransferred(output string) int {
	// Simple parsing - look for "Transferred:" line
	lines := strings.Split(output, "\n")
	for _, line := range lines {
		if strings.Contains(line, "Transferred:") && strings.Contains(line, "file") {
			// Extract number before "file" - basic implementation
			parts := strings.Fields(line)
			for i, part := range parts {
				if strings.Contains(part, "file") && i > 0 {
					// Previous part should be the number
					var count int
					fmt.Sscanf(parts[i-1], "%d", &count)
					return count
				}
			}
		}
	}
	return 0
}

// updateEvidenceJSONFiles efficiently updates each evidence directory's JSON file with sent_start
func (r *RcloneSync) updateEvidenceJSONFiles(validEvidenceDirs []string, sentStart int64, retryCount int) error {
	for _, evidenceDir := range validEvidenceDirs {
		evidenceName := filepath.Base(evidenceDir)
		jsonFile := filepath.Join(evidenceDir, evidenceName+".json")

		// Check if JSON file exists
		if _, err := filepath.Glob(jsonFile); err != nil {
			continue // Skip if JSON file doesn't exist
		}

		// Read existing JSON content
		jsonData, err := ioutil.ReadFile(jsonFile)
		if err != nil {
			continue // Skip on read error
		}

		// Parse JSON
		var data map[string]interface{}
		if err := json.Unmarshal(jsonData, &data); err != nil {
			continue // Skip on parse error
		}

		// Add upload tracking fields
		data["sent_start"] = sentStart
		data["upload_retry_count"] = retryCount

		// Write back to JSON file
		updatedData, err := json.MarshalIndent(data, "", "    ")
		if err != nil {
			continue // Skip on marshal error
		}

		if err := ioutil.WriteFile(jsonFile, updatedData, 0644); err != nil {
			continue // Skip on write error
		}
	}

	return nil
}


