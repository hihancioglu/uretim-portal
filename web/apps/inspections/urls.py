from django.urls import path

from . import views

app_name = "inspections"
urlpatterns = [
    path("", views.home, name="home"),
    path("new/", views.launch, name="new"),
    path("history/", views.history, name="history"),
    path("<uuid:session_id>/", views.detail, name="detail"),
    path("<uuid:session_id>/work/", views.workspace, name="work"),
    path("<uuid:session_id>/eyes/<uuid:eye_id>/requirements/<uuid:requirement_id>/save/", views.measurement_save, name="measurement-save"),
    path("<uuid:session_id>/eyes/<uuid:eye_id>/close/", views.eye_close, name="eye-close"),
    path("<uuid:session_id>/finish/", views.finish, name="finish"),
    path("<uuid:session_id>/eyes/<uuid:eye_id>/visuals/", views.visual_create, name="visual-create"),
    path("<uuid:session_id>/eyes/<uuid:eye_id>/visuals/<uuid:visual_id>/", views.visual_update, name="visual-update"),
    path("<uuid:session_id>/eyes/<uuid:eye_id>/visual-complete/", views.visual_complete, name="visual-complete"),
    path("<uuid:session_id>/finalize/", views.finalize, name="finalize"),
    path("<uuid:session_id>/cancel/", views.cancel, name="cancel"),
    path("<uuid:session_id>/measurements/<uuid:measurement_id>/correct/", views.correct, name="correct"),
]
