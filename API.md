# Lingarr API Documentation

## Overview

Lingarr provides a comprehensive RESTful API that allows you to integrate subtitle translation capabilities into your applications. The API enables you to:

- Authenticate and manage users
- Browse and manage media (movies and TV shows)
- Translate subtitle files and content
- Monitor translation jobs and requests
- Configure application settings
- Access statistics and logs

## Base URL

When running locally or in Docker, the API is available at:
```
http://localhost:9876/api
```

Replace `localhost:9876` with your Lingarr instance URL.

## Interactive API Documentation

Lingarr includes an interactive Swagger UI that provides:
- Complete API endpoint documentation
- Request/response schemas
- Try-it-out functionality
- OpenAPI specification export

Access the Swagger UI at:
```
http://localhost:9876/api/docs
```

## Authentication

Lingarr uses cookie-based authentication. Most endpoints require authentication unless explicitly marked as `[AllowAnonymous]`.

### Login

**Endpoint:** `POST /api/Auth/login`

Authenticate with username and password to receive a session cookie.

**Request Body:**
```json
{
  "username": "your_username",
  "password": "your_password"
}
```

**Response:**
- `200 OK` - Successfully authenticated (cookie set)
- `400 Bad Request` - Missing username or password
- `401 Unauthorized` - Invalid credentials

### Signup (First User Only)

**Endpoint:** `POST /api/Auth/signup`

Create the first user account during initial setup.

**Request Body:**
```json
{
  "username": "admin",
  "password": "secure_password"
}
```

**Response:**
- `200 OK` - User created and authenticated
- `400 Bad Request` - Invalid input or user already exists

### Check Authentication Status

**Endpoint:** `GET /api/Auth/authenticated`

Check if the current session is authenticated and get onboarding status.

**Response:**
```json
{
  "authenticated": true,
  "authType": "Cookie",
  "requiresOnboarding": false
}
```

### Logout

**Endpoint:** `POST /api/Auth/logout`

End the current session.

**Response:**
- `200 OK` - Successfully logged out

### API Key Management

**Endpoint:** `POST /api/Auth/apikey/generate`

Generate a new API key for programmatic access.

**Response:**
```json
{
  "apiKey": "generated-api-key-string"
}
```

## API Endpoints

### Authentication & Users

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| POST | `/api/Auth/login` | Login with credentials | No |
| POST | `/api/Auth/signup` | Create first user | No |
| GET | `/api/Auth/authenticated` | Check auth status | No |
| POST | `/api/Auth/logout` | Logout current session | Yes |
| POST | `/api/Auth/onboarding` | Complete onboarding | No |
| POST | `/api/Auth/apikey/generate` | Generate API key | No |
| GET | `/api/Auth/users` | Get all users | Yes |
| PUT | `/api/Auth/users/{id}` | Update user | Yes |
| DELETE | `/api/Auth/users/{id}` | Delete user | Yes |

### Media Management

#### Movies

**Endpoint:** `GET /api/Media/movies`

Retrieve a paginated list of movies.

**Query Parameters:**
- `searchQuery` (optional): Filter movies by title
- `orderBy` (optional): Sort field (e.g., "Title", "DateAdded")
- `ascending` (default: true): Sort direction
- `pageSize` (default: 20): Items per page
- `pageNumber` (default: 1): Page number

**Response:**
```json
{
  "items": [
    {
      "id": 1,
      "title": "Movie Title",
      "year": 2024,
      "path": "/movies/movie-title",
      "posterPath": "/path/to/poster.jpg"
    }
  ],
  "totalCount": 100,
  "pageNumber": 1,
  "pageSize": 20
}
```

#### TV Shows

**Endpoint:** `GET /api/Media/shows`

Retrieve a paginated list of TV shows.

**Query Parameters:** Same as movies endpoint

**Response:** Similar structure to movies endpoint

#### Exclude Media

**Endpoint:** `POST /api/Media/exclude`

Toggle exclusion status for a media item.

**Request Body:**
```json
{
  "mediaType": "movie",
  "id": 123
}
```

#### Set Translation Threshold

**Endpoint:** `POST /api/Media/threshold`

Set delay before translation starts for new media files.

**Request Body:**
```json
{
  "mediaType": "movie",
  "id": 123,
  "hours": 24
}
```

### Subtitle Translation

#### Translate File

**Endpoint:** `POST /api/Translate/file`

Initiate a translation job for a subtitle file.

**Request Body:**
```json
{
  "path": "/movies/example/subtitle.srt",
  "sourceLanguage": "en",
  "targetLanguage": "es"
}
```

**Response:**
```json
{
  "jobId": "hangfire-job-id"
}
```

#### Translate Line

**Endpoint:** `POST /api/Translate/line`

Translate a single subtitle line immediately.

**Request Body:**
```json
{
  "subtitleLine": "Hello, world!",
  "sourceLanguage": "en",
  "targetLanguage": "es"
}
```

**Response:**
```
¡Hola, mundo!
```

#### Translate Content

**Endpoint:** `POST /api/Translate/content`

Translate subtitle content with batch support.

**Request Body:**
```json
{
  "items": [
    {
      "text": "Line 1",
      "sourceLanguage": "en",
      "targetLanguage": "es"
    },
    {
      "text": "Line 2",
      "sourceLanguage": "en",
      "targetLanguage": "es"
    }
  ]
}
```

**Response:**
```json
[
  {
    "originalText": "Line 1",
    "translatedText": "Línea 1"
  },
  {
    "originalText": "Line 2",
    "translatedText": "Línea 2"
  }
]
```

#### Get Available Languages

**Endpoint:** `GET /api/Translate/languages`

Get list of supported source and target languages.

**Response:**
```json
[
  {
    "code": "en",
    "name": "English",
    "targets": ["es", "fr", "de", "ja"]
  },
  {
    "code": "es",
    "name": "Spanish",
    "targets": ["en", "fr", "de"]
  }
]
```

#### Get Available Models

**Endpoint:** `GET /api/Translate/models`

Get available AI models for the current translation service.

**Response:**
```json
[
  {
    "label": "GPT-4",
    "value": "gpt-4"
  },
  {
    "label": "GPT-3.5 Turbo",
    "value": "gpt-3.5-turbo"
  }
]
```

### Translation Requests

#### Get Active Count

**Endpoint:** `GET /api/TranslationRequest/active`

Get count of active translation requests.

**Response:**
```json
5
```

#### Get Translation Requests

**Endpoint:** `GET /api/TranslationRequest/requests`

Get paginated list of translation requests.

**Query Parameters:**
- `searchQuery` (optional): Filter by search term
- `orderBy` (optional): Sort field
- `ascending` (default: true): Sort direction
- `pageSize` (default: 20): Items per page
- `pageNumber` (default: 1): Page number

**Response:**
```json
{
  "items": [
    {
      "id": 1,
      "path": "/movies/example/subtitle.srt",
      "sourceLanguage": "en",
      "targetLanguage": "es",
      "status": "Processing",
      "createdAt": "2024-01-01T00:00:00Z"
    }
  ],
  "totalCount": 50,
  "pageNumber": 1,
  "pageSize": 20
}
```

#### Cancel Translation Request

**Endpoint:** `POST /api/TranslationRequest/cancel`

Cancel an active translation request.

**Request Body:**
```json
{
  "id": 123
}
```

#### Remove Translation Request

**Endpoint:** `POST /api/TranslationRequest/remove`

Remove a translation request from the list.

**Request Body:**
```json
{
  "id": 123
}
```

#### Retry Translation Request

**Endpoint:** `POST /api/TranslationRequest/retry`

Retry a failed or cancelled translation request.

**Request Body:**
```json
{
  "id": 123
}
```

### Subtitles

#### Get All Subtitles

**Endpoint:** `POST /api/Subtitle/all`

Get list of subtitle files at a specific path.

**Request Body:**
```json
{
  "path": "/movies/example"
}
```

**Response:**
```json
[
  {
    "fileName": "subtitle.en.srt",
    "language": "en",
    "path": "/movies/example/subtitle.en.srt",
    "size": 12345
  },
  {
    "fileName": "subtitle.es.srt",
    "language": "es",
    "path": "/movies/example/subtitle.es.srt",
    "size": 12890
  }
]
```

### Settings

#### Get Setting

**Endpoint:** `GET /api/Setting/{key}`

Retrieve a specific setting value.

**Path Parameters:**
- `key`: The setting key (e.g., "service_type")

**Response:**
```json
"libretranslate"
```

#### Get Multiple Settings

**Endpoint:** `POST /api/Setting/multiple/get`

Retrieve multiple settings at once.

**Request Body:**
```json
["service_type", "source_language", "target_language"]
```

**Response:**
```json
{
  "service_type": "libretranslate",
  "source_language": "en",
  "target_language": "es"
}
```

#### Set Setting

**Endpoint:** `POST /api/Setting`

Update or create a setting.

**Request Body:**
```json
{
  "key": "service_type",
  "value": "deepl"
}
```

#### Set Multiple Settings

**Endpoint:** `POST /api/Setting/multiple/set`

Update or create multiple settings at once.

**Request Body:**
```json
{
  "service_type": "openai",
  "openai_model": "gpt-4",
  "source_language": "en"
}
```

### Path Mappings

#### Get Mappings

**Endpoint:** `GET /api/Mapping/get`

Retrieve all path mappings.

**Response:**
```json
[
  {
    "id": 1,
    "remotePath": "/remote/movies",
    "localPath": "/local/movies"
  }
]
```

#### Set Mappings

**Endpoint:** `POST /api/Mapping/set`

Update path mappings.

**Request Body:**
```json
[
  {
    "remotePath": "/remote/movies",
    "localPath": "/local/movies"
  },
  {
    "remotePath": "/remote/tv",
    "localPath": "/local/tv"
  }
]
```

### Schedule Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/Schedule/jobs` | Get all scheduled jobs |
| POST | `/api/Schedule/job/start` | Start a scheduled job |
| GET | `/api/Schedule/job/automation` | Get automation job status |
| GET | `/api/Schedule/job/movie` | Get movie sync job status |
| GET | `/api/Schedule/job/show` | Get show sync job status |
| DELETE | `/api/Schedule/job/remove/{jobId}` | Remove a scheduled job |
| POST | `/api/Schedule/job/index/movies` | Trigger movie indexing |
| POST | `/api/Schedule/job/index/shows` | Trigger show indexing |

### Statistics

#### Get Statistics

**Endpoint:** `GET /api/Statistics`

Get overall translation statistics.

**Response:**
```json
{
  "totalTranslations": 1000,
  "successfulTranslations": 980,
  "failedTranslations": 20,
  "averageTranslationTime": 45.5
}
```

#### Get Daily Statistics

**Endpoint:** `GET /api/Statistics/daily/{days}`

Get daily statistics for the specified number of days.

**Path Parameters:**
- `days`: Number of days to retrieve (e.g., 7, 30)

**Response:**
```json
[
  {
    "date": "2024-01-01",
    "translations": 50,
    "successful": 48,
    "failed": 2
  },
  {
    "date": "2024-01-02",
    "translations": 45,
    "successful": 44,
    "failed": 1
  }
]
```

### Directory Browsing

**Endpoint:** `GET /api/Directory/get`

Browse directory structure.

**Query Parameters:**
- `path` (optional): Directory path to browse

### Images

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/Image/show/{*path}` | Get TV show image |
| GET | `/api/Image/movie/{*path}` | Get movie image |

### Translation Service Info

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/Translation/languages` | Get supported languages |
| GET | `/api/Translation` | Get translation service info |

### Logs

**Endpoint:** `GET /api/Logs/stream`

Stream application logs (Server-Sent Events).

### Telemetry

**Endpoint:** `GET /api/Telemetry/preview`

Get telemetry preview data.

### Version

**Endpoint:** `GET /api/Version`

Get current version and check for updates.

**Response:**
```json
{
  "currentVersion": "1.0.0",
  "latestVersion": "1.0.1",
  "updateAvailable": true,
  "releaseNotes": "Bug fixes and improvements"
}
```

## Error Handling

The API uses standard HTTP status codes:

| Status Code | Description |
|-------------|-------------|
| 200 OK | Request successful |
| 400 Bad Request | Invalid request parameters or body |
| 401 Unauthorized | Authentication required or failed |
| 404 Not Found | Resource not found |
| 500 Internal Server Error | Server error occurred |

Error responses include a JSON body with details:

```json
{
  "error": "Error message describing what went wrong"
}
```

## Rate Limiting

Currently, the API does not enforce rate limiting, but it's recommended to:
- Avoid excessive concurrent translation requests
- Use the `MAX_CONCURRENT_JOBS` environment variable to control job concurrency
- Monitor translation request status before submitting new requests

## Examples

### Complete Translation Workflow

1. **Authenticate**
```bash
curl -X POST http://localhost:9876/api/Auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"password"}' \
  -c cookies.txt
```

2. **Get Available Languages**
```bash
curl -X GET http://localhost:9876/api/Translate/languages \
  -b cookies.txt
```

3. **Submit Translation Job**
```bash
curl -X POST http://localhost:9876/api/Translate/file \
  -H "Content-Type: application/json" \
  -b cookies.txt \
  -d '{
    "path": "/movies/example/subtitle.srt",
    "sourceLanguage": "en",
    "targetLanguage": "es"
  }'
```

4. **Monitor Translation Requests**
```bash
curl -X GET "http://localhost:9876/api/TranslationRequest/requests?pageSize=10" \
  -b cookies.txt
```

## WebSocket/SignalR Endpoints

Lingarr also provides real-time updates via SignalR:

| Hub | Endpoint | Description |
|-----|----------|-------------|
| TranslationRequests | `/signalr/TranslationRequests` | Real-time translation request updates |
| SettingUpdates | `/signalr/SettingUpdates` | Real-time setting change notifications |
| JobProgress | `/signalr/JobProgress` | Real-time job progress updates |

## Best Practices

1. **Authentication**: Always authenticate before making API calls to protected endpoints
2. **Error Handling**: Implement proper error handling for all API responses
3. **Pagination**: Use pagination parameters for list endpoints to avoid large responses
4. **Path Mappings**: Configure path mappings if your API client has different paths than Lingarr
5. **Monitoring**: Use the statistics and translation request endpoints to monitor system health
6. **Concurrent Jobs**: Respect the `MAX_CONCURRENT_JOBS` setting to avoid overloading the system

## Support

For issues, questions, or contributions:
- GitHub: https://github.com/lingarr-translate/lingarr
- Discord: https://discord.gg/HkubmH2rcR

## License

This API and Lingarr are licensed under the GNU Affero General Public License v3.0.
