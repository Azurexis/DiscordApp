# Paper Animal Adventure Discord Service

A small ASP.NET Core Discord bot service for **Paper Animal Adventure**, hosted on **Azure App Service**.

## Features

- Runs a Discord bot as a hosted background service
- Responds to mentions
- Transforms messages inside the Nana channel
- Sends automated game log messages to a Discord channel
- Exposes internal HTTP endpoints for triggering bot actions
- Uses Azure App Service configuration and Key Vault-backed secrets

## Tech Stack

- C#
- ASP.NET Core
- Discord.Net
- Azure App Service
- GitHub Actions

## Configuration

The app uses environment variables / Azure App Service settings for configuration.

Required settings:

- `discordAPIKey`
- `nanaChannelID`
- `gamelogChannelID`
- `adminChannelID`

In production, secrets should be stored in **Azure Key Vault** and referenced through App Service settings.

## Endpoints

Examples of available endpoints:

- `GET /`
- `GET /health`
- `POST /debugl`

Additional internal endpoints can be used for game event logging.

## Local Development

1. Clone the repository
2. Open the project in Visual Studio
3. Configure the required environment variables or local settings
4. Run the app
5. The bot will connect to Discord and start listening for events

## Deployment

This project is deployed to **Azure App Service**.

Deployment is automated through **GitHub Actions**:
- pushing to `main` triggers a build
- the app is published
- Azure App Service is updated automatically

## Notes

This project is intended as a lightweight internal service / portfolio project and focuses on:
- Azure hosting
- Discord integration
- configuration via environment variables
- CI/CD with GitHub Actions

## Author

Nick Nespor