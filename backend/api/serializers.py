from datetime import datetime, timezone


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def row_timestamp_to_iso(value) -> str:
    if value is None:
        return utc_now_iso()
    if isinstance(value, datetime):
        if value.tzinfo is None:
            value = value.replace(tzinfo=timezone.utc)
        return value.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")
    return str(value)


def target_response(row, status_override=None):
    body = {
        "targetId": row[0],
        "targetName": row[1],
        "displayLabel": row[2],
        "status": status_override or row[4],
        "createdAtUtc": row_timestamp_to_iso(row[5]),
        "targetImageUrl": row[3],
    }
    if len(row) > 6:
        body["vuforiaTargetId"] = row[6] or ""
    if len(row) > 7:
        body["vuforiaStatus"] = row[7] or ""
    if len(row) > 8:
        body["targetReferenceImageUrl"] = row[8] or ""
    return body


def content_response(row, status_override=None):
    return {
        "contentId": row[0],
        "targetId": row[1],
        "status": status_override or row[2],
        "createdAtUtc": row_timestamp_to_iso(row[3]),
    }


def vector_response(x, y, z):
    return {"x": float(x), "y": float(y), "z": float(z)}


def content_detail_response(row):
    return {
        "contentId": row[0],
        "targetId": row[1],
        "contentType": row[2],
        "mediaUrl": row[3],
        "localPosition": vector_response(row[4], row[5], row[6]),
        "localEuler": vector_response(row[7], row[8], row[9]),
        "localScale": vector_response(row[10], row[11], row[12]),
        "renderKind": row[13],
        "assetFormat": row[14],
        "meta": row[15] or {},
        "status": row[16],
        "createdAtUtc": row_timestamp_to_iso(row[17]),
        "updatedAtUtc": row_timestamp_to_iso(row[18]),
    }


def workspace_summary_response(row):
    return {
        "workspaceId": row[0],
        "workspaceName": row[1],
        "state": row[2],
        "schemaVersion": int(row[3]) if row[3] is not None else 1,
        "createdAtUtc": row_timestamp_to_iso(row[4]),
        "updatedAtUtc": row_timestamp_to_iso(row[5]),
        "targetCount": int(row[6]) if row[6] is not None else 0,
        "contentCount": int(row[7]) if row[7] is not None else 0,
        "thumbnailUrl": row[8] or "",
    }


def workspace_detail_response(row):
    return {
        "workspaceId": row[0],
        "workspaceName": row[1],
        "state": row[2],
        "schemaVersion": int(row[3]) if row[3] is not None else 1,
        "createdAtUtc": row_timestamp_to_iso(row[4]),
        "updatedAtUtc": row_timestamp_to_iso(row[5]),
    }


def workspace_target_restore_response(row):
    return {
        "targetId": row[0],
        "workspaceId": row[1],
        "targetName": row[2],
        "displayLabel": row[3],
        "targetImageUrl": row[4] or "",
        "targetReferenceImageUrl": row[5] or "",
        "physicalWidthM": float(row[6]) if row[6] is not None else 1.0,
        "localPosition": vector_response(row[7], row[8], row[9]),
        "localEuler": vector_response(row[10], row[11], row[12]),
        "localScale": vector_response(row[13], row[14], row[15]),
        "vuforiaTargetId": row[16] or "",
        "vuforiaStatus": row[17] or "",
        "status": row[18],
        "createdAtUtc": row_timestamp_to_iso(row[19]),
        "updatedAtUtc": row_timestamp_to_iso(row[20]),
    }


def workspace_content_restore_response(row):
    return {
        "contentId": row[0],
        "targetId": row[1],
        "workspaceId": row[2],
        "contentType": row[3],
        "mediaUrl": row[4] or "",
        "localPosition": vector_response(row[5], row[6], row[7]),
        "localEuler": vector_response(row[8], row[9], row[10]),
        "localScale": vector_response(row[11], row[12], row[13]),
        "renderKind": row[14] or "",
        "assetFormat": row[15] or "",
        "meta": row[16] or {},
        "status": row[17],
        "createdAtUtc": row_timestamp_to_iso(row[18]),
        "updatedAtUtc": row_timestamp_to_iso(row[19]),
    }
