from django.contrib.auth import get_user_model
from django.core.management.base import BaseCommand, CommandError
from apps.accounts.identity import link_identity

class Command(BaseCommand):
    help = "Link a validated OIDC issuer/subject identifier to an existing user."
    def add_arguments(self, parser):
        parser.add_argument("--username", required=True)
        parser.add_argument("--issuer", required=True)
        parser.add_argument("--subject", required=True)
    def handle(self, **options):
        try:
            user = get_user_model().objects.get(username=options["username"])
            link_identity(user=user, issuer=options["issuer"], subject=options["subject"])
        except get_user_model().DoesNotExist as exc:
            raise CommandError("User not found") from exc
        self.stdout.write(self.style.SUCCESS("External identity linked"))
