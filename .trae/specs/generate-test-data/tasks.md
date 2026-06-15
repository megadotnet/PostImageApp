# Tasks
- [x] Task 1: Create TestDataDownloader class in PostImageUploader.Tests/Integration/
  - [x] SubTask 1.1: Create static method that downloads a file from URL and saves to target path
  - [x] SubTask 1.2: Use shared HttpClient instance
  - [x] SubTask 1.3: Skip download if file already exists
- [x] Task 2: Integrate TestDataDownloader into UploadIntegrationTests.cs
  - [x] SubTask 2.1: Call downloader in test constructor or fixture for test_valid.jpg
  - [x] SubTask 2.2: Call downloader in test constructor or fixture for test_valid.png
  - [x] SubTask 2.3: Remove the "skip if file not found" guard (files are now guaranteed)

# Task Dependencies
- Task 2 depends on Task 1
