import logging
import re
import uuid
from .correlation import correlation_id

logger = logging.getLogger("apps.request")
VALID_CORRELATION_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._:-]{0,63}$")

class CorrelationIdMiddleware:
    header = "X-Correlation-ID"

    def __init__(self, get_response):
        self.get_response = get_response

    def __call__(self, request):
        supplied = request.headers.get(self.header, "")
        request_id = supplied if VALID_CORRELATION_ID.fullmatch(supplied) else str(uuid.uuid4())
        token = correlation_id.set(request_id)
        request.correlation_id = request_id
        try:
            response = self.get_response(request)
            response[self.header] = request_id
            logger.info("request.complete", extra={"correlation_id": request_id, "method": request.method, "path": request.path, "status_code": response.status_code})
            return response
        finally:
            correlation_id.reset(token)

