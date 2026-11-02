package config

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"time"
)

type Config struct {
	DeviceID string
	UnitNo   string
	Paths    PathConfig
	MinIO    MinIOConfig
	Sync     SyncConfig
}

type PathConfig struct {
	RecordingFolder string
	ConfigFile      string
	StatusFile      string
	StopFlag        string
	RcloneExe       string
	RcloneConfig    string
}

type MinIOConfig struct {
	Endpoint  string
	Bucket    string
	AccessKey string
	SecretKey string
}

type SyncConfig struct {
	ScanInterval   time.Duration
	StatusInterval time.Duration
	Timeout        time.Duration
	MaxRetries     int
	MaxTransfers   int
}

type SmartdConfig struct {
	DeviceID string `json:"DEVICE_ID"`
	UnitNo   string `json:"UNIT_NO"`
}

func LoadConfig() (*Config, error) {
	// Default paths (mirroring Python version)
	paths := PathConfig{
		RecordingFolder: `D:\pama\ftp\recording`,
		ConfigFile:      `D:\smartd\script\mh02model\files\configs\smartd.json`,
		StatusFile:      `D:\pama\ftp\evidence_status_uploader.json`,
		StopFlag:        `D:\smartd\script\mh02evidenceuploader\evidenceuploadstop.id`,
		RcloneExe:       `D:\smartd\script\rclone\rclone.exe`,
		RcloneConfig:    `./rclone.conf`,
	}

	// Load device info from smartd.json
	smartdConfig, err := loadSmartdConfig(paths.ConfigFile)
	if err != nil {
		return nil, fmt.Errorf("failed to load smartd config: %w", err)
	}

	// Default MinIO config (from rclone.conf equivalent)
	minioConfig := MinIOConfig{
		Endpoint:  os.Getenv("MINIO_ENDPOINT"),
		Bucket:    "evidenceuploader",
		AccessKey: os.Getenv("MINIO_ACCESS_KEY"),
		SecretKey: os.Getenv("MINIO_SECRET_KEY"),
	}

	// Default sync config
	syncConfig := SyncConfig{
		ScanInterval:   5 * time.Second,
		StatusInterval: 30 * time.Second,
		Timeout:        300 * time.Second, // 5 minutes
		MaxRetries:     5,
		MaxTransfers:   8,
	}

	return &Config{
		DeviceID: smartdConfig.DeviceID,
		UnitNo:   smartdConfig.UnitNo,
		Paths:    paths,
		MinIO:    minioConfig,
		Sync:     syncConfig,
	}, nil
}

func loadSmartdConfig(configPath string) (*SmartdConfig, error) {
	file, err := os.Open(configPath)
	if err != nil {
		return nil, fmt.Errorf("failed to open config file: %w", err)
	}
	defer file.Close()

	var config SmartdConfig
	decoder := json.NewDecoder(file)
	if err := decoder.Decode(&config); err != nil {
		return nil, fmt.Errorf("failed to parse config JSON: %w", err)
	}

	if config.DeviceID == "" {
		return nil, fmt.Errorf("DEVICE_ID not found in config")
	}

	return &config, nil
}

func (c *Config) Validate() error {
	// Check recording folder exists
	if _, err := os.Stat(c.Paths.RecordingFolder); os.IsNotExist(err) {
		return fmt.Errorf("recording folder not found: %s", c.Paths.RecordingFolder)
	}

	// Check rclone exe exists
	if _, err := os.Stat(c.Paths.RcloneExe); os.IsNotExist(err) {
		return fmt.Errorf("rclone executable not found: %s", c.Paths.RcloneExe)
	}

	// Check rclone config exists
	configPath, _ := filepath.Abs(c.Paths.RcloneConfig)
	if _, err := os.Stat(configPath); os.IsNotExist(err) {
		return fmt.Errorf("rclone config not found: %s", configPath)
	}

	return nil
}
