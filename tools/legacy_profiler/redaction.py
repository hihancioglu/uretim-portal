"""Central output-safety policy."""

import re

_SENSITIVE = (
    "password", "passwd", "pwd", "passwordhash", "passwordsalt", "token",
    "secret", "authorization", "apikey", "clientsecret", "privatekey",
    "decryptkey", "connectionstring", "databaseurl", "credential",
)
_PERSONAL = ("username", "operatorname", "createdby", "changedby", "email", "phone")


def normalized_name(value: str) -> str:
    return re.sub(r"[^a-z0-9]", "", value.casefold())


def is_sensitive(name: str) -> bool:
    candidate = normalized_name(name)
    return any(term in candidate for term in _SENSITIVE)


def may_sample(name: str) -> bool:
    candidate = normalized_name(name)
    return not is_sensitive(name) and not any(term in candidate for term in _PERSONAL)
