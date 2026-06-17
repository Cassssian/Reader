# Configuration Supabase (100 % gratuit, sans CB)

Supabase offre un **free tier permanent** : 500 Mo de base, 1 Go de Storage, 50 000
requêtes Auth/mois — largement suffisant pour un usage personnel.

## 1. Créer un projet

1. <https://supabase.com> → **Start for free** → **New project** (plan Free).
2. Noter l'**URL du projet** (ex. `https://abcdef.supabase.co`) et la **clé anon**.

## 2. Renseigner les constantes

```csharp
// Services/SupabaseService.cs
public const string Url     = "https://abcdef.supabase.co";
public const string AnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...";
```

## 3. Créer les tables (SQL Editor → New query)

```sql
-- Bibliothèque de romans
create table novels (
  id           text primary key,
  user_id      uuid references auth.users not null,
  url          text,
  title        text,
  author       text,
  cover        text,
  custom_cover text,
  description  text,
  ep_count     int  default 0,
  last_ep      int  default 0,
  progress     float default 0,
  updated      bigint default 0
);

-- Préférences utilisateur
create table settings (
  user_id uuid primary key references auth.users,
  dark    boolean default true,
  lang    text    default 'en',
  speed   float   default 1.0,
  mode    int     default 1,
  updated bigint  default 0
);
```

## 4. Règles RLS (chaque utilisateur ne voit que ses données)

```sql
-- Activer RLS
alter table novels   enable row level security;
alter table settings enable row level security;

-- Politique novels
create policy "own novels" on novels
  using (auth.uid() = user_id)
  with check (auth.uid() = user_id);

-- Politique settings
create policy "own settings" on settings
  using (auth.uid() = user_id)
  with check (auth.uid() = user_id);
```

## 5. Bucket Storage (couvertures personnalisées)

1. **Storage** → **New bucket** → nom : `covers` → **Public** (lecture publique, écriture via RLS).
2. Politique Storage :

```sql
-- Écriture : uniquement le propriétaire
create policy "upload own cover" on storage.objects
  for insert with check (
    bucket_id = 'covers' and
    auth.uid()::text = (storage.foldername(name))[1]
  );
```

## 6. Google OAuth

1. **Authentication → Providers → Google** → activer.
2. Créer un client OAuth dans [Google Cloud Console](https://console.cloud.google.com/apis/credentials) :
   - Type : **Web application**
   - URIs de redirection autorisés : `https://abcdef.supabase.co/auth/v1/callback`
3. Coller **Client ID** et **Client Secret** dans Supabase.
4. Dans le projet, ajouter `readerapp://auth` aux **Redirect URLs** (Authentication → URL Configuration).

## 7. La sync est optionnelle

L'app fonctionne **entièrement hors ligne** (SQLite local). La connexion Supabase
n'est requise que pour synchroniser la progression entre appareils.
