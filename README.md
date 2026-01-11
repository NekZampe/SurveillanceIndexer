# Surveillance Indexer

Surveillance Indexer is a video management system designed to ingest surveillance footage, analyze it using computer vision, and index events for retrieval. The application uses a YOLOv3 model to identify and track objects (such as people and vehicles), storing these events in a relational database for query-based playback.

![Application Screenshot](https://github.com/user-attachments/assets/5496e29f-3ce9-4561-9d74-c982e510b49d)

It's still under development, I will update it with progress.

## Features
* **Drag-and-Drop Ingestion:** Batch processing queue with duplicate detection via MD5 hashing.
* **Object Detection:** Real-time object tracking using YOLOv3 and OpenCV.
* **Event Indexing:** Logs start/end times and confidence levels to a local database.
* **Playback System:** Searchable dashboard to review specific event clips.

## Tech Stack
* **Language:** C# (.NET 8)
* **UI:** WPF (MVVM Architecture)
* **Computer Vision:** OpenCvSharp4, YOLOv3
* **Data Access:** Entity Framework Core
* **Concurrency:** Task Parallel Library (TPL)
