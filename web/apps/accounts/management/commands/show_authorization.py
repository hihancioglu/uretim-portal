from django.contrib.auth import get_user_model
from django.core.management.base import BaseCommand, CommandError
from apps.accounts.authz.services import _assignments
class Command(BaseCommand):
    help = "Inspect active application roles, actions, and scopes for a user."
    def add_arguments(self, parser): parser.add_argument("--username", required=True)
    def handle(self, **options):
        try: user=get_user_model().objects.get(username=options["username"])
        except get_user_model().DoesNotExist as exc: raise CommandError("User not found") from exc
        for assignment in _assignments(user).prefetch_related("role__permission_links__permission", "scope_grants"):
            actions=sorted(link.permission.code for link in assignment.role.permission_links.all() if link.permission.is_active)
            scopes=sorted(f"{scope.scope_type}:{scope.scope_key}" for scope in assignment.scope_grants.all() if scope.is_active)
            self.stdout.write(f"{assignment.role.code}: actions={','.join(actions)} scopes={','.join(scopes)}")
