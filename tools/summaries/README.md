# tools/summaries/

How the history summaries in `summaries.json` are made. Only the merge step is included as code here.
The extraction scripts I used were tied to one-off working folders, state files and absolute paths,
so I left them out for now. They'll be rewritten as clean tools when the MySQL + PHP API work starts.

## Pipeline

1. **Pick places.** Query the NPS NRHP MapServer (`cultural_resources/nrhp_locations/MapServer/0`) for a city or area
   (for example `City='ELLICOTT CITY' AND State='MARYLAND'`) or the N nearest points to a location.
2. **Find the nomination PDF.** In order: the NARA catalog copy
   (`catalog.archives.gov/medialz/electronic-records/rg-079/NPS_<ST>/{ref}.pdf`), the NPGallery `_Text.pdf`
   (`npgallery.nps.gov/AssetDetail/NRIS/{ref}`), then the state historic preservation office
   (Alabama Historical Commission attachments, Maryland Historical Trust Medusa PDFs).
3. **Extract text.** `pdftotext -layout` per page; pages with no text layer are rendered with `pdftoppm` and OCR'd with
   `tesseract`. Locate the significance section (Statement of Significance / Summary Paragraph / Narrative Statement).
4. **Write the summary.** An AI writes a short plain-language summary (about 60-130 words) from that section only:
   what the place is, when and by whom it was built, and why it was listed. It gets a confidence rating (high/medium)
   and is spot-checked against the source. The `heading` field records which section it came from.
5. **Merge.** `merge_summaries.py new_entries.json` merges into `web/summaries.json` and the Android
   `app/src/main/assets/summaries.json`, validates, and keeps both identical.
6. **Deploy.** `tools/deploy/upload.sh web/summaries.json`.
