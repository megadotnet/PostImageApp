# Generate Test Data Files Spec

## Why
The integration tests (`UploadIntegrationTests.cs`) require `test_valid.jpg` and `test_valid.png` in the `testdata/` directory. Currently these files must be manually obtained. This spec defines a C# test initializer that downloads them from known URLs before the upload tests run.

## What Changes
- Add a test data downloader class (`TestDataDownloader`) that uses `HttpClient` to download images from source URLs and save them to `testdata/`.
- Add an xUnit test fixture or constructor logic that runs before the integration tests to ensure the files exist.
- The downloaded files shall be saved as `testdata/test_valid.jpg` and `testdata/test_valid.png`.

## Impact
- Affected specs: None
- Affected code: `PostImageUploader.Tests/Integration/` (new downloader class), `UploadIntegrationTests.cs` (add initialization)
- Affected tests: `UploadIntegrationTests.cs` — tests will no longer skip when files are present

## ADDED Requirements
### Requirement: Download test_valid.jpg from SCUT mirror
The system SHALL download the JPEG image from `https://www2.scut.edu.cn/_upload/tpl/00/f1/241/template241/images/head1_2.jpg` and save it as `testdata/test_valid.jpg` before the upload integration test runs.

#### Scenario: Download succeeds
- **WHEN** `UploadValidJpg_ReturnsPostimgCcUrl` test starts
- **THEN** `testdata/test_valid.jpg` exists and is a valid JPEG image

### Requirement: Download test_valid.png from GitHub
The system SHALL download the PNG image from `https://test-images.github.io/png/202105/cs-black-000.png` and save it as `testdata/test_valid.png` before the upload integration test runs.

#### Scenario: Download succeeds
- **WHEN** `UploadValidPng_ReturnsPostimgCcUrl` test starts
- **THEN** `testdata/test_valid.png` exists and is a valid PNG image
