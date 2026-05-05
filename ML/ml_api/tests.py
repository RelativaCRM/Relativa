from datetime import date
from unittest.mock import patch

from django.test import TestCase
from rest_framework.test import APIRequestFactory

from ml_api.apps import MlApiConfig
from ml_api.views import score_batch


class _ModelStub:
    def __init__(self, probability):
        self.probability = probability

    def predict_proba(self, _inputs):
        return [[1 - self.probability, self.probability]]


class ScoreBatchTests(TestCase):
    def setUp(self):
        self.factory = APIRequestFactory()
        MlApiConfig.closure_model = _ModelStub(0.88)
        MlApiConfig.churn_model = _ModelStub(0.11)

    def tearDown(self):
        MlApiConfig.closure_model = None
        MlApiConfig.churn_model = None

    def test_rejects_invalid_payload(self):
        request = self.factory.post("/api/ml/score/batch", {}, format="json")
        response = score_batch(request)
        self.assertEqual(response.status_code, 400)

    def test_rejects_non_integer_ids(self):
        request = self.factory.post("/api/ml/score/batch", {"entity_ids": [1, "x"]}, format="json")
        response = score_batch(request)
        self.assertEqual(response.status_code, 400)

    def test_returns_503_when_models_missing(self):
        MlApiConfig.closure_model = None
        request = self.factory.post("/api/ml/score/batch", {"entity_ids": [1]}, format="json")
        response = score_batch(request)
        self.assertEqual(response.status_code, 503)

    @patch("ml_api.views._load_contract_inputs")
    @patch("ml_api.views._load_deal_inputs")
    @patch("ml_api.views._load_analysis_state")
    @patch("ml_api.views._ensure_deal_analysis_entities")
    @patch("ml_api.views._load_schema_config")
    def test_happy_path_scores(
        self,
        schema_mock,
        ensure_mock,
        analysis_mock,
        deal_mock,
        contract_mock,
    ):
        schema_mock.return_value = {"type_ids": {}, "rel_ids": {}, "prop_ids": {}}
        ensure_mock.return_value = None
        analysis_mock.side_effect = [
            {
                101: {
                    "analysis_entity_id": 2001,
                    "days_since_created": 12,
                    "stage_encoded": 2,
                    "num_interactions": 6,
                    "days_since_last_contact": 4,
                    "num_open_deals": 1,
                    "avg_deal_value": 1500.0,
                    "source_updated_at": date.today(),
                    "calculated_at": date.today(),
                }
            }
        ]
        deal_mock.return_value = {}
        contract_mock.return_value = []

        request = self.factory.post("/api/ml/score/batch", {"entity_ids": [101]}, format="json")
        response = score_batch(request)
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.data[0]["entity_id"], 101)
        self.assertIsInstance(response.data[0]["closure_score"], float)
        self.assertIsInstance(response.data[0]["churn_score"], float)

    @patch("ml_api.views._load_contract_inputs")
    @patch("ml_api.views._load_deal_inputs")
    @patch("ml_api.views._load_analysis_state")
    @patch("ml_api.views._ensure_deal_analysis_entities")
    @patch("ml_api.views._load_schema_config")
    def test_missing_analysis_returns_null_scores(
        self,
        schema_mock,
        ensure_mock,
        analysis_mock,
        deal_mock,
        contract_mock,
    ):
        schema_mock.return_value = {"type_ids": {}, "rel_ids": {}, "prop_ids": {}}
        ensure_mock.return_value = None
        analysis_mock.return_value = {}
        deal_mock.return_value = {}
        contract_mock.return_value = []

        request = self.factory.post("/api/ml/score/batch", {"entity_ids": [999]}, format="json")
        response = score_batch(request)
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.data[0]["closure_score"], None)
        self.assertEqual(response.data[0]["churn_score"], None)

    @patch("ml_api.views._recompute_analysis")
    @patch("ml_api.views._load_contract_inputs")
    @patch("ml_api.views._load_deal_inputs")
    @patch("ml_api.views._load_analysis_state")
    @patch("ml_api.views._ensure_deal_analysis_entities")
    @patch("ml_api.views._load_schema_config")
    def test_stale_analysis_recompute_path(
        self,
        schema_mock,
        ensure_mock,
        analysis_mock,
        deal_mock,
        contract_mock,
        recompute_mock,
    ):
        schema_mock.return_value = {"type_ids": {}, "rel_ids": {}, "prop_ids": {}}
        ensure_mock.return_value = None
        analysis_mock.side_effect = [
            {
                77: {
                    "analysis_entity_id": 707,
                    "days_since_created": 10,
                    "stage_encoded": 2,
                    "num_interactions": 5,
                    "days_since_last_contact": 7,
                    "num_open_deals": 1,
                    "avg_deal_value": 1000.0,
                    "source_updated_at": date.today(),
                    "calculated_at": date(2000, 1, 1),
                }
            },
            {
                77: {
                    "analysis_entity_id": 707,
                    "days_since_created": 11,
                    "stage_encoded": 3,
                    "num_interactions": 6,
                    "days_since_last_contact": 8,
                    "num_open_deals": 2,
                    "avg_deal_value": 1200.0,
                    "source_updated_at": date.today(),
                    "calculated_at": date.today(),
                }
            },
        ]
        deal_mock.return_value = {}
        contract_mock.return_value = []
        recompute_mock.return_value = None

        request = self.factory.post("/api/ml/score/batch", {"entity_ids": [77]}, format="json")
        response = score_batch(request)
        self.assertEqual(response.status_code, 200)
        self.assertTrue(recompute_mock.called)

    @patch("ml_api.views._load_schema_config")
    def test_timeout_returns_504(self, _schema_mock):
        request = self.factory.post("/api/ml/score/batch", {"entity_ids": [1]}, format="json")
        with patch("ml_api.views._check_deadline", side_effect=TimeoutError()):
            response = score_batch(request)
        self.assertEqual(response.status_code, 504)
