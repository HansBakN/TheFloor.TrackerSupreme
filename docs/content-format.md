# Quiz content format

The application reads UTF-8 YAML from the external data directory. Set `THEFLOOR_DATA_DIR` before starting the process to override the operating system default. Choose a private directory outside the Git checkout and separate from the server’s public web root.

| System | Default |
| --- | --- |
| macOS | `~/Library/Application Support/TheFloor.TrackerSupreme` |
| Windows | `%LOCALAPPDATA%\TheFloor.TrackerSupreme` |
| Linux | `$XDG_DATA_HOME/TheFloor.TrackerSupreme`, or `~/.local/share/TheFloor.TrackerSupreme` |

Put one `category.yaml` per directory under `library/categories/`, and one `set.yaml` per directory under `sets/`. The directory names can be descriptive. UUIDs identify content and must stay the same when you rename an existing category, set, question, or directory. Generate a new UUID for each new item. The list order defines display and question order. The application stores its active set selection in `state/app.db`.

## Category

```yaml
schemaVersion: 1
id: 8ecaf3a0-742d-4acf-8dad-896f6bbc9b1e
name: Around the world
description: Cities, landscapes and sounds
coverImage: images/cover.png
questions:
  - id: 163d3cda-f8af-4d15-a0ec-a61f7ef788fa
    type: text
    prompt: What is the capital of Denmark?
    answer: Copenhagen
    alternatives: [København]
    enabled: true
  - id: 5374adfb-d935-42cb-8861-a13e26642898
    type: image
    prompt: Name this landmark.
    media: images/tower.jpg
    answer: Eiffel Tower
    notes: Accept the French name.
    alternatives: [Tour Eiffel]
    enabled: true
  - id: 3f40955a-9162-4a37-a317-f01c421422e3
    type: audio
    prompt: Identify this sound.
    media: audio/bicycle-bell.ogg
    answer: Bicycle bell
    enabled: true
```

Use lowercase question types. Every question needs an answer, text questions need a prompt, and image and audio questions need a media path. Optional fields can be omitted. Questions default to enabled. Quote scalars that YAML might read as other values, such as a numeric answer or text containing a colon followed by a space. YAML tags, anchors and aliases are unsupported.

Media paths use forward slashes and resolve inside the category directory. Use only ASCII letters, digits, periods, hyphens and underscores per path segment. Media must be at most 24 MiB per file. Supported extensions are JPG, JPEG, PNG, WebP, GIF, MP3, WAV and OGG. Uploading a replacement creates a separate file so another question can retain its existing media. Deleting a category removes its definition and set references. Its media files remain in its former directory for recovery.

## Set

```yaml
schemaVersion: 1
id: 13e52fd7-df9e-472f-bafc-d07d9d7e65ed
name: Family Birthday
description: The evening's categories
categoryIds:
  - 8ecaf3a0-742d-4acf-8dad-896f6bbc9b1e
  - 26475b62-549f-456c-a516-63d5ea16aadd
```

Each category UUID needs a corresponding category definition. A second set can include the same UUID in its own list to share the category. To make an independent copy manually, copy the folder and its media, assign a new category UUID and new question UUIDs, and reference that category UUID in the desired set.

## Editing safely

Save files as UTF-8. The application watches the category and set directories and reports validation errors. A running process retains its last valid version of a definition if an external edit makes it unreadable. If a library directory becomes unavailable, restore access and use **Reload files** before editing. If the UI reports that a file changed while you were editing, reload its latest version before reapplying the edit.

## Portable packages

In **Category sets**, choose **Export set ZIP** to create a self-contained package. In **Categories**, choose **Export category ZIP** to share one category with its media. Import the file from the matching editor.

A category ZIP has `category.yaml` and the relative media paths referenced by the definition. A set ZIP uses this structure:

```text
set.yaml
categories/
  8ecaf3a0-742d-4acf-8dad-896f6bbc9b1e/
    category.yaml
    images/cover.png
    images/tower.jpg
    audio/bicycle-bell.ogg
  26475b62-549f-456c-a516-63d5ea16aadd/
    category.yaml
```

Category directory names inside a set ZIP match the lowercase UUIDs in `set.yaml`. Include each referenced category and all media referenced by its cover or questions. The application rejects unsafe paths, symbolic links, case-insensitive duplicate entries, and unexpected files. ZIP packages are limited to 128 MiB compressed and expanded, with at most 10,000 entries. The per-definition limit is 4 MiB and the per-media-file limit is 24 MiB.

An import reuses a category only when its definition and referenced media are identical. For an ID conflict, the editor offers **Import independent copies**. The importer assigns new UUIDs to conflicting categories and their questions and remaps the imported set's references. The original definitions and media stay in the library.

Text-only categories without a cover can use JSON. The JSON file is one category object with the same field names and schema version as YAML.

```json
{
  "schemaVersion": 1,
  "id": "26475b62-549f-456c-a516-63d5ea16aadd",
  "name": "Capitals",
  "questions": [
    {
      "id": "93dc6ed4-b264-4c5f-a3e3-b55af2d675fa",
      "type": "text",
      "prompt": "What is the capital of Denmark?",
      "answer": "Copenhagen",
      "alternatives": ["København"],
      "enabled": true
    }
  ]
}
```

## Backup and transfer

Stop the server and copy the entire external data directory, including `library`, `sets`, and `state`, to a separate location. To restore, stop the destination server, move its current data directory aside, and copy the backup into the configured data location before starting. Copying the entire directory preserves set selection, saved duel state, and snapshot media. Use a set ZIP when transferring just one event's content.


## Interrupted operations

The server resumes an interrupted set import or shared category deletion from its recovery plan in `state`. It applies only steps that still match their expected revisions and checks already applied steps before continuing. If an external edit conflicts, the library shows the affected path and pauses writes. Preserve your edited copy separately, reconcile the file with the staged operation, and select **Reload files**. Back up the whole data root before manually moving or editing recovery files.

## Category reset backups

In **Categories**, choose **Reset all categories** to empty the entire library. Confirmation names every category and affected set, regardless of the current search. All sets remain, with empty category lists; the active-set selection and existing duel snapshots are preserved. Resolve any library errors before resetting. A stale confirmation is rejected; reload and review the scope again.

Each reset moves complete category folders, including media and unused uploads, into `backups/category-reset-<unique-id>/categories/` under the configured data directory. Original affected `set.yaml` files are saved under the same backup's `sets/` directory. The success message shows the backup's relative path. These backups contain private event content and should be kept outside Git.

To restore, stop the server and copy the backed-up category folders into `library/categories/`. Copy the backed-up set folders into `sets/` if you also want their original memberships. Back up the current data directory first, and resolve folder or UUID collisions without overwriting newer content. Start the server and check the library. To restore only one category, copy only its category folder, then use the UI to add it to sets. Retain the backup until the restored library has been checked.

If reset is interrupted, the recovery journal under `state/content-op-…` resumes it on restart or reload. A conflicting external edit stops recovery and reports the affected path. Preserve that edit and the reset backup before resolving the conflict and reloading; do not delete a pending journal.
