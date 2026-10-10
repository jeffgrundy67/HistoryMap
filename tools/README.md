# tools/

| Folder | What's in it |
|---|---|
| `photos/` | Scripts that pull one exterior photo per place from nomination PDFs (code included) |
| `summaries/` | How summaries are produced (README) plus `merge_summaries.py` to merge new summaries into both apps |
| `deploy/` | cPanel upload helpers. Credentials come only from the environment variables `CPANEL_HOST`, `CPANEL_USER`, `CPANEL_API_TOKEN` |

The scripts are Bash and Python 3. On Windows run them from Git Bash or WSL.
