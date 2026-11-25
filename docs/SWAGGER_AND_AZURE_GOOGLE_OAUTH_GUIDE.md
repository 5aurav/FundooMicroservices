# Swagger, Azure VM Domain Name & Google OAuth Guide

This document describes how to use the unified Swagger UI across all Fundoo microservices, how to configure an Azure VM domain name, and how to set up and test Google OAuth 2.0 end-to-end.

---

## 1. Unified Swagger UI

The **API Gateway** (`Fundoo.ApiGateway`) now hosts a centralized Swagger UI console that aggregates all microservices into a single dashboard.

### How to Access Swagger
- **Locally**: `http://localhost:7000/swagger` (or root `http://localhost:7000`)
- **Docker Compose**: `http://<azure-vm-ip-or-domain>:7000/swagger`
- **Kubernetes (NodePort)**: `http://<azure-vm-ip-or-domain>:30080/swagger`
- **Individual Services Direct**:
  - User Service: `http://localhost:7001/swagger`
  - Notes Service: `http://localhost:7002/swagger`
  - Notification Service: `http://localhost:7003/swagger`

> [!NOTE]
> In all microservices (`User`, `Notes`, `Notification`, and `ApiGateway`), Swagger UI is now enabled in all environments (including `Production` and Docker/Kubernetes containers), resolving the previous issue where Swagger was only available in `Development`.

### Swagger Definition Dropdown
In the top-right corner of Swagger UI, use the **Select a definition** dropdown to switch between:
1. **User API v1**: Authentication, Registration, Google OAuth, 2FA, Sessions.
2. **User API v2**: Enhanced login with Remember-Me and Refresh Token rotation.
3. **Notes API**: Notes CRUD, Pin, Archive, Trash, Color coding, and Labels.
4. **Notification API**: Email notification triggers and idempotency tracking.

### Authorizing in Swagger
1. Call `POST /api/v1/auth/login` (or register, or use Google OAuth).
2. Copy the `accessToken` from the JSON response.
3. Click the green **Authorize** button at the top-right of Swagger UI.
4. Enter `Bearer <your_token>` (or just `<your_token>`) and click **Authorize**.
5. All authorized endpoints (`/api/notes`, `/api/labels`, `/api/v1/auth/sessions`, etc.) will now automatically include the JWT bearer token.

---

## 2. Setting Up an Azure VM Domain Name

Google OAuth 2.0 **strictly forbids raw IP addresses** (e.g. `http://20.55.123.45:7000`) as Authorized Redirect URIs. It requires a domain name or `localhost`. Azure provides a free, instant Fully Qualified Domain Name (FQDN) for any VM public IP.

### Step-by-Step Azure VM DNS Configuration:
1. Log in to the [Azure Portal](https://portal.azure.com).
2. Go to **Virtual machines** and select your VM.
3. In the VM **Overview** page, locate **Public IP address** and click on the IP link.
4. On the Public IP resource blade, click **Configuration** under the **Settings** menu on the left.
5. In the **DNS name label (optional)** box, enter a unique name (e.g., `fundoo-api-saurav`).
6. Click **Save**.
7. Your domain name is now active:
   ```
   <your-label>.<region>.cloudapp.azure.com
   ```
   *Example:* `fundoo-api-saurav.eastus.cloudapp.azure.com`

### Opening Inbound Ports in Azure Network Security Group (NSG):
1. In your VM page, click **Networking** (or **Network settings**).
2. Click **Add inbound port rule**:
   - **Destination port ranges**: `7000, 30080, 80, 443`
   - **Protocol**: `TCP`
   - **Action**: `Allow`
   - **Priority**: `1010`
   - **Name**: `Allow_Fundoo_Gateway`
3. Click **Add**.

---

## 3. Configuring Google Cloud Console for OAuth 2.0

1. Go to the [Google Cloud Console](https://console.cloud.google.com/).
2. Select or create a project (e.g. `Fundoo-Microservices`).
3. Navigate to **APIs & Services** > **OAuth consent screen**:
   - Choose **External** user type and click **Create**.
   - Fill in **App name** (e.g., `Fundoo Notes`), **User support email**, and **Developer contact email**.
   - Under **Scopes**, ensure `.../auth/userinfo.email`, `.../auth/userinfo.profile`, and `openid` are selected.
   - Add your email under **Test users** so you can log in while in Testing mode.
4. Navigate to **APIs & Services** > **Credentials**:
   - Click **Create Credentials** > **OAuth client ID**.
   - Application type: **Web application**.
   - Name: `Fundoo Web Client`.
   - **Authorized JavaScript origins**:
     - `http://localhost:7000`
     - `http://<your-label>.<region>.cloudapp.azure.com:7000`
     - *(If using Kubernetes NodePort)*: `http://<your-label>.<region>.cloudapp.azure.com:30080`
   - **Authorized redirect URIs**:
     - `http://localhost:7000/api/v1/auth/google/callback`
     - `http://<your-label>.<region>.cloudapp.azure.com:7000/api/v1/auth/google/callback`
     - *(If using Kubernetes NodePort)*: `http://<your-label>.<region>.cloudapp.azure.com:30080/api/v1/auth/google/callback`
5. Click **Create** and copy your:
   - **Client ID**
   - **Client Secret**

---

## 4. Configuring Fundoo with Google Credentials

### Option A: Via `appsettings.json` in `Fundoo.User.Api`
```json
"Google": {
  "ClientId": "YOUR_CLIENT_ID.apps.googleusercontent.com",
  "ClientSecret": "GOCSPX-your-secret",
  "RedirectUri": "http://<your-label>.<region>.cloudapp.azure.com:7000/api/v1/auth/google/callback"
}
```

### Option B: Via Environment Variables (Docker Compose / Kubernetes)
In `docker-compose.yml` or your `.env` file:
```env
GOOGLE_CLIENT_ID=YOUR_CLIENT_ID.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=GOCSPX-your-secret
GOOGLE_REDIRECT_URI=http://<your-label>.<region>.cloudapp.azure.com:7000/api/v1/auth/google/callback
```

In Kubernetes (`fundoo-k8s.yaml` under `user-deploy`):
```yaml
- name: Google__ClientId
  value: "YOUR_CLIENT_ID.apps.googleusercontent.com"
- name: Google__ClientSecret
  value: "GOCSPX-your-secret"
- name: Google__RedirectUri
  value: "http://<your-label>.<region>.cloudapp.azure.com:30080/api/v1/auth/google/callback"
```

---

## 5. Testing Google OAuth 2.0 End-to-End

### Step 1: Get the Google Authorization URL
Call `GET /api/v1/auth/google/url` in Swagger UI or curl:
```bash
curl http://<your-domain>:7000/api/v1/auth/google/url
```
Response:
```json
{
  "isSuccess": true,
  "authorizationUrl": "https://accounts.google.com/o/oauth2/v2/auth?client_id=...&response_type=code&scope=openid%20profile%20email&redirect_uri=http%3A%2F%2F<your-domain>%3A7000%2Fapi%2Fv1%2Fauth%2Fgoogle%2Fcallback&state=...&access_type=offline&prompt=consent",
  "state": "..."
}
```

### Step 2: Open Authorization URL in Browser
Paste the `authorizationUrl` into your browser. Google will show the consent screen. Choose your Google account and grant permissions.

### Step 3: Verify the Automatic Callback
Google automatically redirects your browser to:
`http://<your-domain>:7000/api/v1/auth/google/callback?code=4/0Abc...&state=...`

The endpoint will respond with:
```json
{
  "isSuccess": true,
  "userId": "c3b5fa5c-b5f7-4a0b-9df2-5fa624d6ce74",
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "sessionId": "1e780491-b3b0-4f51-b8be-683a48e77a28",
  "tokenType": "Bearer",
  "message": "Google authentication successful."
}
```

### Step 4: Verify in Swagger
1. Copy the `accessToken`.
2. Open `http://<your-domain>:7000/swagger`.
3. Click **Authorize** and paste your token.
4. Select definition **Notes API** and try `GET /api/notes` or `POST /api/notes`.
5. You are fully authenticated using Google OAuth!
