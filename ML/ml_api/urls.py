from django.urls import path

from ml_api import views

urlpatterns = [
    path('api/ml/health/', views.health, name='ml-health'),
    path('api/ml/recalculate/', views.recalculate, name='ml-recalculate'),
]
