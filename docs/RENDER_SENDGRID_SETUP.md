# Render + SendGrid Email Setup

This app sends email through SMTP (SendGrid recommended).

## 1) Create SendGrid API Key

1. Log in to SendGrid.
2. Go to `Settings` -> `API Keys` -> `Create API Key`.
3. Choose restricted access and enable only `Mail Send` (minimum required).
4. Copy the key once (starts with `SG.`).

## 2) Verify Sender

1. In SendGrid, open `Settings` -> `Sender Authentication`.
2. Verify either:
   - a single sender email, or
   - your sending domain (recommended for production).
3. Use that verified email as the `From` address in Render env vars.

## 3) Configure Render Environment Variables

Set these in your Render service dashboard:

```env
Smtp__Host=smtp.sendgrid.net
Smtp__Port=587
Smtp__Username=apikey
Smtp__Password=SG_xxxxxxxxxxxxxxxxxxxxxxxxxxxxx
Smtp__From=verified-sender@yourdomain.com
Smtp__FromName=KASAH QMS
Smtp__EnableSsl=true
Application__BaseUrl=https://your-app.onrender.com
```

Optional fallback key supported by the app:

```env
SENDGRID_API_KEY=SG_xxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

Compatibility note: the app also supports `EmailSettings__*` keys, but `Smtp__*` is the primary configuration.

## 4) Redeploy

After saving env vars, trigger a new deploy in Render.

## 5) Verify End-to-End

Test the following in production:

1. Forgot password -> user receives reset link email.
2. Create user with reset-link option -> user receives account setup email.
3. Create/share task -> assignee receives in-app + email.
4. Create/share document (user/department) -> recipients receive in-app + email.
5. Create document from template (shared) -> recipients receive in-app + email.
6. Edit a document and change sharing target -> new recipients receive in-app + email.
7. Publish news -> users receive in-app + email.

## 6) Troubleshooting

- If emails are not sent, check Render logs for:
  - `[NO SMTP CONFIGURED]`
  - SMTP authentication errors (invalid key or sender not verified)
  - TLS/port mismatch (use `587` with SSL enabled as configured above)
- Confirm `Application__BaseUrl` is the real public URL so password-reset links are correct.

## Security Reminder

Do not commit real API keys to git. If any key was exposed, rotate it immediately in SendGrid and update Render env vars.
