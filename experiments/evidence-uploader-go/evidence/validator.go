package evidence

import (
	"bufio"
	"encoding/json"
	"os"
	"path/filepath"
	"strconv"
	"strings"
)

type Validator struct{}

func NewValidator() *Validator {
	return &Validator{}
}

// CheckIfZeroKPH validates if evidence has zero speed (should be filtered out)
// Returns true if speed is zero (should skip), false if speed > 0 (should process)
func (v *Validator) CheckIfZeroKPH(evidenceDir *EvidenceDir) bool {
	evidenceName := evidenceDir.Name
	jsonPath := filepath.Join(evidenceDir.Path, evidenceName+".json")
	txtPath := filepath.Join(evidenceDir.Path, evidenceName+".txt")

	// First try JSON file
	if _, err := os.Stat(jsonPath); err == nil {
		if isZeroFromJSON, found := v.checkSpeedFromJSON(jsonPath); found {
			return isZeroFromJSON
		}
	}

	// Fallback to TXT file
	if _, err := os.Stat(txtPath); err == nil {
		return v.checkSpeedFromTXT(txtPath)
	}

	// If we can't determine speed, assume it's valid (not zero KPH)
	return false
}

func (v *Validator) checkSpeedFromJSON(jsonPath string) (bool, bool) {
	file, err := os.Open(jsonPath)
	if err != nil {
		return false, false
	}
	defer file.Close()

	var metadata EvidenceMetadata
	decoder := json.NewDecoder(file)
	if err := decoder.Decode(&metadata); err != nil {
		return false, false
	}

	// Check if speed field exists and equals "0"
	if metadata.Speed != "" {
		return metadata.Speed == "0", true
	}

	return false, false
}

func (v *Validator) checkSpeedFromTXT(txtPath string) bool {
	file, err := os.Open(txtPath)
	if err != nil {
		return false // Assume not zero KPH if can't read
	}
	defer file.Close()

	maxSpeed := "0"
	scanner := bufio.NewScanner(file)

	for scanner.Scan() {
		line := strings.TrimSpace(scanner.Text())
		if line == "" {
			continue
		}

		fields := strings.Split(line, ",")
		if len(fields) < 8 {
			continue
		}

		// Check if this is an "ON" record (field[3])
		if strings.TrimSpace(fields[3]) == "ON" {
			// Speed is in field[7], remove quotes
			speedField := strings.Trim(strings.TrimSpace(fields[7]), "'\"")

			// Compare speeds as strings (matching Python logic)
			if v.compareSpeed(speedField, maxSpeed) > 0 {
				maxSpeed = speedField
			}
		} else if strings.TrimSpace(fields[3]) == "POST" {
			// Stop processing at POST record
			break
		}
	}

	// Return true if max speed is "0" (should skip)
	return maxSpeed == "0"
}

// compareSpeed compares two speed strings
// Returns: -1 if a < b, 0 if a == b, 1 if a > b
func (v *Validator) compareSpeed(a, b string) int {
	// Try to parse as numbers for proper comparison
	aFloat, aErr := strconv.ParseFloat(a, 64)
	bFloat, bErr := strconv.ParseFloat(b, 64)

	if aErr == nil && bErr == nil {
		if aFloat < bFloat {
			return -1
		} else if aFloat > bFloat {
			return 1
		}
		return 0
	}

	// Fallback to string comparison
	if a < b {
		return -1
	} else if a > b {
		return 1
	}
	return 0
}