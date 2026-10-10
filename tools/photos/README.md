# tools/photos/

Scripts used to pull one exterior photo per place out of the National Register nomination PDFs.
Run them from a scratch working folder (they read and write files in the current directory).
Downloaded PDFs and page renders are temporary; delete them afterward.

Needs Python 3 with `requests`, `Pillow`, `numpy`, plus poppler-utils (`pdfinfo`, `pdftoppm`, `pdftotext`).

| Step | Script | Does |
|---|---|---|
| 1 | `fetch.py` | Downloads NPGallery photo PDFs for the refs in `places.json` |
| 2 | `classify.py` | Scores every page so photo pages stand out from text, forms and maps |
| 3 | `crop.py` | Crops, resizes (max 1000 px) and saves `{ref}.jpg` at 50-150 KB; manual overrides in `picks.json` |
| 4 | `caption.py` | Suggests captions from the text layer (reviewed by hand) |
| - | `pagesheet.py`, `sheet.py`, `stack.py` | Contact sheets for a quick visual check |

Rules I followed: the first clear exterior photo of the building or site; skip maps, forms and sketch plans.
If NPGallery has no usable photo, fall back to the NARA combined PDF or the state historic preservation office's file
(the Maryland Historical Trust, for example), and pass that PDF through the `pdf` override in `picks.json`.
The results are merged into `web/photos.json` as `{"ref": {"file": "photos/{ref}.jpg", "source": "<pdf url>", "caption": "..."}}`.
