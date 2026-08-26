from django.core.management.base import BaseCommand
from apps.accounts.seeding import seed_authorization_baseline
class Command(BaseCommand):
    help = "Idempotently create the approved WP-002 role/action baseline."
    def handle(self, **options):
        seed_authorization_baseline()
        self.stdout.write(self.style.SUCCESS("Authorization baseline seeded"))
