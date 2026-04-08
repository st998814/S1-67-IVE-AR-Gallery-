# Backend Testing Instruction — T3.22b

This guide explains how to run the backend exception handling and error logging tests locally using VS Code.

---

## Prerequisites

Make sure you have the following installed:

- Python 3.8+
- `pip` packages: `flask`, `flask-cors`, `psycopg2-binary`, `pytest`, `werkzeug`

Install dependencies (if not already):

```bash
pip install flask flask-cors psycopg2-binary pytest werkzeug
```

---

## Running the Tests

1. Open the project folder in VS Code
2. Open the integrated terminal (`Ctrl + `` ` ``)
3. Run:

```bash
pytest backend/test_app.py -v
```

You should see output like:

```
backend/test_app.py::test_404_returns_json              PASSED
backend/test_app.py::test_405_returns_json              PASSED
backend/test_app.py::test_upload_no_file_part           PASSED
backend/test_app.py::test_upload_empty_filename         PASSED
backend/test_app.py::test_upload_success                PASSED
backend/test_app.py::test_upload_oserror_returns_500    PASSED
backend/test_app.py::test_serve_file_not_found          PASSED
backend/test_app.py::test_content_not_json              PASSED
backend/test_app.py::test_content_missing_fields        PASSED
backend/test_app.py::test_content_db_error_returns_500  PASSED
backend/test_app.py::test_content_unexpected_error_returns_500 PASSED
backend/test_app.py::test_content_success               PASSED
backend/test_app.py::test_db_error_is_logged            PASSED
backend/test_app.py::test_unexpected_error_is_logged    PASSED
```

---

## What Is Being Tested

| Test | Description |
|------|-------------|
| `test_404_returns_json` | Unknown routes return a JSON 404 error |
| `test_405_returns_json` | Wrong HTTP method returns a JSON 405 error |
| `test_upload_no_file_part` | Upload with no file returns 400 |
| `test_upload_empty_filename` | Upload with empty filename returns 400 |
| `test_upload_success` | Valid file upload returns 201 with a URL |
| `test_upload_oserror_returns_500` | Disk error during upload returns 500 |
| `test_serve_file_not_found` | Requesting a missing file returns 404 |
| `test_content_not_json` | Non-JSON request body returns 400 |
| `test_content_missing_fields` | Missing required fields returns 400 |
| `test_content_db_error_returns_500` | Database error returns 500 |
| `test_content_unexpected_error_returns_500` | Unexpected server error returns 500 |
| `test_content_success` | Valid payload returns 201 with an ID |
| `test_db_error_is_logged` | Database errors are written to the log |
| `test_unexpected_error_is_logged` | Unexpected errors are written to the log |

---

## Notes

- These tests **do not require a real database connection** — database calls are mocked.
- A `server.log` file will be created in the `backend/` folder during testing. This is normal and is excluded from Git.
- If a test fails, check the error message in the terminal — it will indicate which assertion failed and why.
