package evidence

import (
	"os"
	"path/filepath"
	"regexp"
	"strings"
	"time"
)

type Scanner struct {
	recordingDir string
}

func NewScanner(recordingDir string) *Scanner {
	return &Scanner{
		recordingDir: recordingDir,
	}
}

func (s *Scanner) ScanReadyEvidenceDirs() ([]*EvidenceDir, error) {
	var readyDirs []*EvidenceDir

	// Check if recording directory exists
	if _, err := os.Stat(s.recordingDir); os.IsNotExist(err) {
		return readyDirs, nil
	}

	// Walk through all subdirectories to find evidence dirs
	err := filepath.WalkDir(s.recordingDir, func(path string, d os.DirEntry, err error) error {
		if err != nil {
			return nil // Skip errors, continue walking
		}

		if !d.IsDir() {
			return nil
		}

		// Skip the root recording directory itself
		if path == s.recordingDir {
			return nil
		}

		dirName := d.Name()

		// Apply filtering rules from Python version
		if !s.isValidEvidenceDir(dirName) {
			return nil
		}

		evidenceDir := &EvidenceDir{
			Name: dirName,
			Path: path,
		}

		// Get directory info
		if info, err := d.Info(); err == nil {
			evidenceDir.LastModified = info.ModTime()
		}

		// Check if evidence is complete and get device creation time
		files, isComplete, deviceCreationTime := s.checkEvidenceComplete(path, dirName)
		evidenceDir.Files = files
		evidenceDir.IsComplete = isComplete
		evidenceDir.DeviceCreationTime = deviceCreationTime

		if isComplete {
			readyDirs = append(readyDirs, evidenceDir)
		}

		return nil
	})

	return readyDirs, err
}

func (s *Scanner) isValidEvidenceDir(dirName string) bool {
	// Must have more than 2 underscores (matching Python: len(item.split("_")) > 2)
	if len(strings.Split(dirName, "_")) <= 2 {
		return false
	}

	// Exclude directories with "TESTING" in name
	if strings.Contains(strings.ToUpper(dirName), "TESTING") {
		return false
	}

	// Exclude numeric-only directories (8 digits pattern)
	matched, _ := regexp.MatchString(`^[0-9]{8}$`, dirName)
	if matched {
		return false
	}

	return true
}

func (s *Scanner) checkEvidenceComplete(dirPath, evidenceName string) ([]string, bool, int64) {
	var foundFiles []string
	var oldestModTime int64 = time.Now().Unix()

	// Check for required files: {name}.mp4, {name}.json, {name}.jpg, {name}.txt
	for _, fileType := range RequiredFileTypes {
		fileName := evidenceName + "." + fileType
		filePath := filepath.Join(dirPath, fileName)

		if fileInfo, err := os.Stat(filePath); err == nil {
			foundFiles = append(foundFiles, fileName)

			// Get oldest file ModTime (device creation time)
			fileModTime := fileInfo.ModTime().Unix()
			if fileModTime < oldestModTime {
				oldestModTime = fileModTime
			}
		}
	}

	// Evidence is complete if all 4 required files exist
	isComplete := len(foundFiles) == len(RequiredFileTypes)

	return foundFiles, isComplete, oldestModTime
}