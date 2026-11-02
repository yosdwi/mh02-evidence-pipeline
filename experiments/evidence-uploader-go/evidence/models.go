package evidence

import (
	"time"
)

type EvidenceDir struct {
	Name         string
	Path         string
	Files        []string
	IsComplete   bool
	IsZeroKPH    bool
	LastModified time.Time
	DeviceCreationTime int64 // Device file creation time (epoch)
}

type EvidenceMetadata struct {
	Speed string `json:"speed"`
}

var RequiredFileTypes = []string{"mp4", "json", "jpg", "txt"}