# Jellyfin Plugin Audible

Plugin Jellyfin pour récupérer les métadonnées des audiobooks depuis Audible via [audnex.us](https://api.audnex.us).

## Fonctionnalités

- Résumé / description
- Auteurs et narrateurs
- Série et numéro de tome
- Genres
- Couverture
- Note communautaire
- Date de parution, éditeur

## Prérequis

- Jellyfin 10.11.x (.NET 9)

## Installation

1. Télécharger `Jellyfin.Plugin.Audible.dll` depuis les [releases](https://github.com/HellTruckerfr/jellyfin-plugin-audible/releases)
2. Créer le dossier `C:\ProgramData\Jellyfin\Server\plugins\Audible_10.11.8.0\` (Windows) ou l'équivalent sur Linux/Docker
3. Y copier le `.dll` et le `meta.json` suivant :

```json
{
  "guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "name": "Audible",
  "targetAbi": "10.11.0.0",
  "version": "1.0.0.0",
  "status": "Active",
  "category": "Books"
}
```

4. Redémarrer Jellyfin
5. Dans les paramètres de la bibliothèque Audiobook, ajouter **Audible** comme provider de métadonnées pour le type `AudioBook`

## Configuration

Dans **Tableau de bord → Plugins → Audible**, choisir la région (défaut : `fr`).

Régions disponibles : `fr`, `de`, `uk`, `us`, `it`, `es`, `ca`, `au`, `jp`

## Fonctionnement

Le plugin interroge [api.audnex.us](https://api.audnex.us) (API publique, sans authentification) pour récupérer les métadonnées par ASIN.

Si l'ASIN n'est pas connu, il effectue une recherche dans le catalogue Audible par titre + auteur, filtrée par langue selon la région configurée — ce qui évite de récupérer des éditions dans une autre langue.

## Compilation

```bash
dotnet build -c Release
```

Nécessite les DLLs de Jellyfin Server dans `C:\Program Files\Jellyfin\Server\` (chemin configurable dans le `.csproj`).

## Licence

MIT
