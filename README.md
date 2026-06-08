# Otakarr

**Otakarr** is a stateless C# Newznab-compatible Indexer that bridges the *Arr stack (Sonarr/Radarr) and web-based streaming scrapers. It acts as an indexer in Sonarr/Radarr, searches various streaming platforms via custom scrapers, and encapsulates the stream metadata into a Base64-encoded URL payload sent directly to a SABnzbd-compatible download client like [Riparr](https://github.com/fofola1/Riparr).

---

## Features
- **Newznab Compliant**: Emulates capabilities (`t=caps`) and TV Search (`t=tvsearch`), matching the *Arr stack standard.
- **TVmaze Metadata Resolution**: Resolves external show IDs (TVDB or IMDb) sent by Sonarr/Radarr into clean search queries.
- **Stateless Integration**: Encodes the entire download contract (scraper name, episode, stream url, etc.) in a URL parameter (`payload=`) inside the XML `<enclosure url="...">` field.
- **Microservice-oriented**: Fully Docker-ready and light on resources.

---

## Docker Compose Setup

Below is the recommended configuration to deploy **Otakarr** and **Riparr** together in a unified network:

```yaml
services:
  otakarr:
    image: ghcr.io/fofola1/otakarr:latest
    container_name: otakarr
    restart: unless-stopped
    ports:
      - "8000:8000"
    environment:
      - PORT=8000
      - DOWNLOADER_URL=http://riparr:8080/api/sabnzbd
      - API_KEY=your_shared_indexer_token
    # env_file:
    #   - .env

  riparr:
    image: ghcr.io/fofola1/riparr:latest
    container_name: riparr
    restart: unless-stopped
    ports:
      - "8080:8080"
    volumes:
      - ./downloads:/downloads
    environment:
      - API_KEY=riparr-token
      - TZ=Europe/Bratislava
      - PORT=8080
```

---

## Configuration

| Environment Variable | Default Value | Description |
|---|---|---|
| `PORT` | `8000` | Port the web service listens on. |
| `DOWNLOADER_URL` | `http://localhost:8080/download` | The base URL of the downloader service (Riparr) where download payloads will be sent. |
| `API_KEY` | `null` | API key required in the query parameter (`apikey=`) to authorize incoming Sonarr requests. |

---

## Developer Run

To compile and run the project locally on your host:

```bash
# Clone the repository
cd AniOwn-Indexer

# Run the project
dotnet run
```

Ensure `Otakarr` can reach `Riparr` on the path defined in `DOWNLOADER_URL`.

---

## Sonarr Setup

1. In Sonarr, navigate to **Settings > Indexers > Add (+)**.
2. Select **Newznab** (Custom).
3. Set the following fields:
   - **Name**: `Otakarr`
   - **URL**: `http://localhost:8000/api` (or container IP)
   - **API Key**: The value set in `API_KEY` env variable (e.g. `your_shared_indexer_token`).
   - **Categories**: `5070` (TV/Anime), `2070` (Movies/Anime)
4. Click **Test** and **Save**.
