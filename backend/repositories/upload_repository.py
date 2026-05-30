class UploadRepository:
    def remove_existing_for_stored_name(self, cur, stored_file_name: str):
        cur.execute("DELETE FROM uploads WHERE stored_file_name = %s;", (stored_file_name,))

    def insert_upload(self, cur, *, file_name, stored_file_name, mime_type, size_bytes, url):
        cur.execute(
            """
            INSERT INTO uploads (file_name, stored_file_name, mime_type, size_bytes, url)
            VALUES (%s, %s, %s, %s, %s);
            """,
            (file_name, stored_file_name, mime_type, size_bytes, url),
        )
