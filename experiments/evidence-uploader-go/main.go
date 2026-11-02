package main

import (
	"context"
	"log"
	"os"
	"os/signal"
	"sync"
	"syscall"
	"time"

	"evidence-uploader/config"
	"evidence-uploader/status"
	syncpkg "evidence-uploader/sync"
)

func main() {
	log.Println("Starting Evidence Uploader v2.0.0 (Go)")

	// Load configuration
	cfg, err := config.LoadConfig()
	if err != nil {
		log.Fatalf("Failed to load configuration: %v", err)
	}

	// Validate configuration
	if err := cfg.Validate(); err != nil {
		log.Fatalf("Configuration validation failed: %v", err)
	}

	log.Printf("Configuration loaded successfully for device: %s", cfg.DeviceID)

	// Create context for graceful shutdown
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	// Setup signal handling
	sigCh := make(chan os.Signal, 1)
	signal.Notify(sigCh, syscall.SIGINT, syscall.SIGTERM)

	// Create sync worker
	syncWorker := syncpkg.NewWorker(cfg)

	// Create status reporter
	statusReporter := status.NewReporter(cfg)

	// Start components
	var wg sync.WaitGroup

	// Start sync worker
	wg.Add(1)
	go func() {
		defer wg.Done()
		if err := syncWorker.Start(ctx); err != nil && err != context.Canceled {
			log.Printf("Sync worker error: %v", err)
		}
	}()

	// Start status reporter
	wg.Add(1)
	go func() {
		defer wg.Done()
		if err := statusReporter.Start(ctx, syncWorker); err != nil && err != context.Canceled {
			log.Printf("Status reporter error: %v", err)
		}
	}()

	// Monitor stop flag file
	wg.Add(1)
	go func() {
		defer wg.Done()
		monitorStopFlag(ctx, cfg.Paths.StopFlag, cancel)
	}()

	log.Println("Evidence uploader started successfully")

	// Wait for shutdown signal
	select {
	case sig := <-sigCh:
		log.Printf("Received signal: %v", sig)
	case <-ctx.Done():
		log.Println("Context cancelled")
	}

	log.Println("Initiating graceful shutdown...")

	// Cancel context to signal all goroutines to stop
	cancel()

	// Wait for all goroutines to finish with timeout
	done := make(chan struct{})
	go func() {
		wg.Wait()
		close(done)
	}()

	select {
	case <-done:
		log.Println("All components stopped gracefully")
	case <-time.After(10 * time.Second):
		log.Println("Shutdown timeout reached, forcing exit")
	}

	log.Println("Evidence uploader stopped")
}

func monitorStopFlag(ctx context.Context, stopFlagPath string, cancel context.CancelFunc) {
	ticker := time.NewTicker(1 * time.Second)
	defer ticker.Stop()

	for {
		select {
		case <-ctx.Done():
			return
		case <-ticker.C:
			if _, err := os.Stat(stopFlagPath); err == nil {
				log.Printf("Stop flag detected: %s", stopFlagPath)

				// Remove the stop flag file
				if err := os.Remove(stopFlagPath); err != nil {
					log.Printf("Failed to remove stop flag: %v", err)
				} else {
					log.Println("Stop flag removed")
				}

				// Trigger shutdown
				cancel()
				return
			}
		}
	}
}