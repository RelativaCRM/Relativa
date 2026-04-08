from rest_framework.decorators import api_view
from rest_framework.response import Response


@api_view(['POST'])
def recalculate(request):
    return Response({'status': 'accepted', 'detail': 'stub'})
