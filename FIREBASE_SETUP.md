# Firebase setup

Reader talks to Firebase over **REST** (no native SDKs), so setup is just a few
values + security rules. No `google-services.json` / `GoogleService-Info.plist`
wiring is required.

## 1. Create the project
1. Go to <https://console.firebase.google.com> → **Add project**.
2. Enable **Authentication → Google** provider.
3. Create a **Firestore** database (production mode).
4. Enable **Storage**.

## 2. Get your values
In **Project settings → General**:

| Constant in `Services/FirebaseService.cs` | Where to find it |
|---|---|
| `ApiKey`         | Web API Key |
| `ProjectId`      | Project ID |
| `Bucket`         | `default-storage-bucket` (`<project-id>.appspot.com`) |
| `GoogleClientId` | APIs & Services → Credentials → **OAuth 2.0 Web client** ID |

Set the OAuth **Authorized redirect URI** to `readerapp://auth`
(the custom scheme is already declared in every platform manifest/Info.plist).

```csharp
// Services/FirebaseService.cs
const string ApiKey         = "AIza...";
const string ProjectId      = "reader-12345";
const string Bucket         = "reader-12345.appspot.com";
const string GoogleClientId = "xxxxx.apps.googleusercontent.com";
```

## 3. Firestore data model
```
users/{uid}
  novels/{novelId}   -> { url,title,author,cover,customCover,desc,epCount,lastEp,progress,updated }
  meta/settings      -> { dark,lang,speed,mode,updated }
```

## 4. Firestore rules — each user only touches their own tree
```
rules_version = '2';
service cloud.firestore {
  match /databases/{db}/documents {
    match /users/{uid}/{document=**} {
      allow read, write: if request.auth != null && request.auth.uid == uid;
    }
  }
}
```

## 5. Storage rules — custom covers under covers/{uid}/...
```
rules_version = '2';
service firebase.storage {
  match /b/{bucket}/o {
    match /covers/{uid}/{file} {
      allow read: if true;                                   // covers are public
      allow write: if request.auth != null && request.auth.uid == uid;
    }
  }
}
```

## Notes
- **Sync strategy**: last-write-wins on the `updated` (epoch ms) field. Local SQLite
  is the source of truth offline; `SyncService.Pull()` merges on sign-in.
- **Theme/reader prefs** also live in local `Preferences` for instant startup, then
  mirror to `users/{uid}/meta/settings`.
