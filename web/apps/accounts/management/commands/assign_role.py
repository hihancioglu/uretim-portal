from django.contrib.auth import get_user_model
from django.core.management.base import BaseCommand, CommandError
from apps.accounts.models import Role, UserRole
from apps.accounts.seeding import seed_assignment_scopes
from apps.audit.services import create_audit_event
class Command(BaseCommand):
    def add_arguments(self, parser):
        parser.add_argument("--username", required=True); parser.add_argument("--role", required=True); parser.add_argument("--deactivate", action="store_true")
    def handle(self, **options):
        try:
            user=get_user_model().objects.get(username=options["username"]); role=Role.objects.get(code=options["role"])
        except (get_user_model().DoesNotExist, Role.DoesNotExist) as exc: raise CommandError("User or role not found") from exc
        assignment,_=UserRole.objects.get_or_create(user=user, role=role)
        assignment.is_active=not options["deactivate"]; assignment.save(update_fields=["is_active","updated_at"])
        if assignment.is_active: seed_assignment_scopes(assignment)
        create_audit_event(event_type="auth.user_role_revoked" if options["deactivate"] else "auth.user_role_assigned", entity_type="accounts.UserRole", entity_id=str(assignment.pk), metadata={"user_id":str(user.pk),"role_code":role.code})
