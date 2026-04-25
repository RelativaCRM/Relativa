from rest_framework.decorators import api_view
from rest_framework.response import Response
from .apps import MlApiConfig
import os


@api_view(['GET'])
def health(request):
    # Перевіряємо, чи моделі успішно завантажились у пам'ять
    model_loaded = (MlApiConfig.churn_model is not None) and (MlApiConfig.closure_model is not None)
    return Response({'status': 'ok', 'model_loaded': model_loaded})


@api_view(['POST'])
def recalculate(request):
    return Response({'status': 'accepted', 'detail': 'stub'})
