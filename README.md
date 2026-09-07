# Raven

> **Système intelligent de planification et suivi des visites de maintenance préventive et curative (ECS Maintenance)**

Raven est une application d'ingénierie logicielle complète construite avec **ASP.NET Core (.NET 10)**, **EF Core**, **SQL Server** et une interface web moderne temps réel. Elle intègre un moteur de scoring algorithmique pour la priorisation des visites, un pipeline ETL transactionnel pour l'import de données Excel réelles, et un module d'authentification sécurisé par JWT.

---

## 🏗️ Architecture & Modèle Relationnel

```
               ┌──────────────┐
               │    Client    │
               └──────┬───────┘
                      │ 1:N
        ┌─────────────┴─────────────┐
        ▼                           ▼
┌──────────────┐             ┌──────────────┐
│     Site     │             │    Marche    │
└───────┬──────┘             └──────┬───────┘
        │ 1:N                       │ 1:N
        ▼                           │
┌──────────────┐                    │
│  Equipement  │                    │
└───────┬──────┘                    │
        │ 1:N                       │
        ▼                           │
┌──────────────┐ ◄──────────────────┘
│    Visite    │ ◄─── (FK TechnicienId) ─── ┌──────────────┐
└──────────────┘                            │  Technicien  │
                                            └──────────────┘
```

### Entités principales

- **`Client`** : Entreprise cliente avec code unique (`CL-XXX-0001`), coordonnées et sites associés.
- **`Site`** : Localisation géographique (`ST-XXX-0001`), ville, coordonnées GPS.
- **`Marche`** : Contrat de maintenance (`MAR-YYYY-0001`), SLA, périodicité, PV requis, type de contrat.
- **`Equipement`** : Équipements critiques (`EQ-XXX-0001`), scores de santé et de risque, historique d'interventions.
- **`Visite`** : Visite de maintenance avec référence séquentielle (`VIS-YYYY-0001`), score de priorité calculé, rapport technique et clé étrangère `TechnicienId`.
- **`Technicien`** : Technicien terrain avec spécialités, charge de travail hebdomadaire et disponibilité.
- **`Utilisateur`** : Comptes d'authentification avec rôles (`Responsable`, `Technicien`) et hachage sécurisé.

---

## ⚡ Fonctionnalités Clés

1. **Pipeline ETL Excel Transactionnel** :
   - Upload de fichiers Excel métiers réels.
   - Prévisualisation et validation des données (`POST /api/marches/import/preview`).
   - Import atomique avec transaction SQL (`POST /api/marches/import/confirm`) : déduplication automatique, création des clients, sites, marchés et parcs d'équipements.

2. **Algorithme de Scoring Multi-Critères** :
   - **Score de Priorité Visite (0-100)** : basé sur la criticité de l'équipement, l'âge de l'installation, le dépassement du délai et le type d'intervention.
   - **Score d'Affectation Technicien (0-100)** : adéquation des compétences, proximité géographique, disponibilité et équilibrage de charge.

3. **Espace Technicien Terrain & Rapports** :
   - Endpoint dédié `GET /api/visites/mes-visites`.
   - Saisie de fiches d'intervention et actions correctives.
   - Recalcul automatique de la prochaine visite préventive selon le contrat.

4. **Exports Multi-Formats** :
   - Génération de Procès-Verbaux (PV) en PDF via **QuestPDF**.
   - Exports du planning et des marchés en **Excel (.xlsx)** et **CSV**.

5. **Sécurité & JWT** :
   - Authentification JWT avec validation d'émetteur/audience et tokens signés.
   - Gestion de session et protection des routes.

---

## 🚀 Démarrage Rapide

### Prérequis
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- SQL Server (local ou via Docker)

### Lancement en local

1. **Configurer la base de données dans `appsettings.json` :**
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost,1433;Database=RavenDB;User Id=sa;Password=VotreMotDePasse;TrustServerCertificate=True;"
   }
   ```

2. **Appliquer les migrations EF Core :**
   ```bash
   dotnet ef database update
   ```

3. **Lancer l'application :**
   ```bash
   dotnet run
   ```
   L'application est accessible sur `http://localhost:5196`.

### Lancement avec Docker

```bash
docker-compose up --build
```
L'API est accessible sur `http://localhost:8080`.

---

## 📂 Structure du Codebase

```
Controllers/          # Contrôleurs API REST (Marches, Visites, Equipements, Clients, Techniciens, Auth, Dashboard)
Data/                 # AppDbContext et configurations Fluent API avec index uniques
Models/               # Entités du domaine relationnel
Services/             # Logique métier (ScoringService, ExcelImportService, PdfExportService, CsvExportService)
Migrations/           # Migrations EF Core
wwwroot/              # Interface utilisateur (index.html, styles.css, app.js, fallback.js)
```

