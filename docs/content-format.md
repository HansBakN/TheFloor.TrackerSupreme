# Quiz content format

The application reads UTF-8 YAML from the external data directory. Set `THEFLOOR_DATA_DIR` before starting the process to override the operating system default.

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

Save files as UTF-8. The application watches the category and set directories and reports validation errors. A running process retains its last valid version of a definition if an external edit makes it unreadable. Reload after correcting the file. If the UI reports that a file changed while you were editing, reload its latest version before reapplying the edit.
