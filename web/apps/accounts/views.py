from django.conf import settings
from django.contrib.auth import logout
from django.http import HttpResponse, HttpResponseRedirect
from django.urls import reverse

def login_start(request):
    if not settings.OIDC_ENABLED:
        return HttpResponse("OIDC girişi yapılandırılmamış.", status=503)
    return HttpResponseRedirect(reverse("oidc_authentication_init"))

def logout_view(request):
    logout(request)
    return HttpResponseRedirect(settings.OIDC_POST_LOGOUT_REDIRECT_URI or "/")

def access_denied(request):
    return HttpResponse("Erişim reddedildi.", status=403)
