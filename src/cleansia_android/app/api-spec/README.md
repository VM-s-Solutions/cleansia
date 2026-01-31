# API Specification

This directory contains the OpenAPI/Swagger specification file used to generate the Kotlin API client.

## Usage

### Download the latest API spec from running backend:
```bash
./gradlew downloadApiSpec
```

### Generate the API client:
```bash
./gradlew generateApiClient
```

### Download and generate in one step:
```bash
./gradlew updateApiClient
```

## Notes

- The `swagger.json` file is downloaded from the backend's Swagger endpoint
- Make sure the backend is running at `http://localhost:5000` before downloading
- Generated code is placed in `build/generated/openapi/`
- The generated source is automatically included in the build
