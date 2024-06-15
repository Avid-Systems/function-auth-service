# TokenExchange Azure Function

## Overview

The `TokenExchange` Azure Function is designed to exchange a token issued for a Single Page Application (SPA) for access tokens to interact with Dataverse API and Microsoft Graph API. The function accepts a token and tenant ID, then provides the requested tokens based on specified resources.

## Endpoint

### POST `/api/TokenExchange`

Exchanges a provided token for access tokens to Dataverse and/or Microsoft Graph API.

#### Request

##### Headers

- `Content-Type: application/json`

##### Body

- `tenantid`: The tenant ID of the Azure AD.
- `token`: The token issued for your SPA.
- `orgurl`: The organization URL for Dataverse (if Dataverse token is requested).
- `resources`: Comma-separated string specifying requested resources (dataverse, graph, or both).

```json
{
  "tenantid": "string",
  "token": "string",
  "orgurl": "string",
  "resources": "string"
}
```

##### Example

```json
{
  "tenantid": "your-tenant-id",
  "token": "your-spa-token",
  "orgurl": "https://your-org.crm.dynamics.com",
  "resources": "dataverse,graph"
}
```

##### Responses

###### 200 OK

- `dataversetoken`: The access token for Dataverse API (if requested)..
- `graphtoken`: The access token for Graph API (if requested).

```json
{
  "dataversetoken": "string",
  "graphtoken": "string"
}
```

###### 400 Bad Request

```json
{
  "error": "string"
}
```

func new --name TokenExchange --template "HTTP trigger" --authlevel "function"
