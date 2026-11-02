package status

import "time"

type SyncStats struct {
	TotalSyncs      int
	SuccessfulSyncs int
	FailedSyncs     int
	LastSyncTime    time.Time
	LastError       string
}