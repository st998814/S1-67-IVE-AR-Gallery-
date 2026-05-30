from flask import jsonify


def error_response(message: str, error_code: str, status_code: int, details=None):
    body = {"message": message, "errorCode": error_code}
    if details is not None:
        body["details"] = details
    return jsonify(body), status_code
