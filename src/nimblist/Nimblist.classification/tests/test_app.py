import unittest
import sys
import os
import numpy as np
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from app import app
import json
from unittest.mock import patch, MagicMock
import app as app_module


def _make_primary_mock(categories, proba_row):
    """Return a mock primary model that returns the given probabilities."""
    m = MagicMock()
    m.classes_ = np.array(categories)
    m.predict_proba.return_value = np.array([proba_row])
    return m


def _make_sub_mock(categories, proba_row):
    m = MagicMock()
    m.classes_ = np.array(categories)
    m.predict_proba.return_value = np.array([proba_row])
    return m


class TestApp(unittest.TestCase):
    def setUp(self):
        self.app = app.test_client()
        self.app.testing = True

    # ------------------------------------------------------------------
    # Health check
    # ------------------------------------------------------------------
    def test_health_check(self):
        response = self.app.get('/health')
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.json, {'status': 'ok'})

    # ------------------------------------------------------------------
    # Input validation
    # ------------------------------------------------------------------
    def test_predict_missing_json(self):
        response = self.app.post('/predict', data=None)
        self.assertEqual(response.status_code, 400)
        self.assertIn('error', response.json)

    def test_predict_invalid_json(self):
        response = self.app.post('/predict', data='notjson', content_type='application/json')
        self.assertEqual(response.status_code, 400)
        self.assertIn('error', response.json)

    def test_predict_missing_product_name(self):
        response = self.app.post('/predict', json={})
        self.assertEqual(response.status_code, 400)
        self.assertIn('error', response.json)

    def test_predict_endpoint_exists(self):
        response = self.app.post('/predict', json={'item': 'test item'})
        self.assertNotEqual(response.status_code, 404)

    # ------------------------------------------------------------------
    # Models not loaded
    # ------------------------------------------------------------------
    def test_predict_models_not_loaded(self):
        with patch.object(app_module, 'primary_model', None), \
             patch.object(app_module, 'primary_vectorizer', None):
            response = self.app.post('/predict', json={'product_name': 'milk'})
            self.assertEqual(response.status_code, 500)
            self.assertIn('error', response.json)

    # ------------------------------------------------------------------
    # Confidence threshold — below threshold returns null
    # ------------------------------------------------------------------
    @patch.object(app_module, 'primary_model')
    @patch.object(app_module, 'primary_vectorizer')
    def test_predict_below_confidence_threshold_returns_null_category(self, mock_vec, mock_model):
        """When max probability < PRIMARY_CONFIDENCE_THRESHOLD, category should be null."""
        mock_vec.transform.return_value = [[0]]
        # Spread probabilities evenly across 4 classes — none will reach 35%
        mock_model.classes_ = np.array(['Bakery', 'Dairy', 'Fresh & Chilled', 'Frozen'])
        mock_model.predict_proba.return_value = np.array([[0.26, 0.25, 0.25, 0.24]])

        with patch.object(app_module, 'PRIMARY_CONFIDENCE_THRESHOLD', 0.35):
            response = self.app.post('/predict', json={'product_name': 'ambiguous item'})

        self.assertEqual(response.status_code, 200)
        self.assertIsNone(response.json['predicted_primary_category'])
        self.assertIsNone(response.json['predicted_sub_category'])

    @patch.object(app_module, 'primary_model')
    @patch.object(app_module, 'primary_vectorizer')
    def test_predict_above_confidence_threshold_returns_category(self, mock_vec, mock_model):
        """When max probability >= PRIMARY_CONFIDENCE_THRESHOLD, category should be returned."""
        mock_vec.transform.return_value = [[0]]
        mock_model.classes_ = np.array(['Bakery', 'Fresh & Chilled'])
        mock_model.predict_proba.return_value = np.array([[0.85, 0.15]])

        with patch.object(app_module, 'PRIMARY_CONFIDENCE_THRESHOLD', 0.35), \
             patch.dict(app_module.sub_models, {}, clear=True), \
             patch.dict(app_module.sub_vectorizers, {}, clear=True):
            response = self.app.post('/predict', json={'product_name': 'bread'})

        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.json['predicted_primary_category'], 'Bakery')

    # ------------------------------------------------------------------
    # Sub-model missing / low confidence
    # ------------------------------------------------------------------
    @patch.object(app_module, 'primary_model')
    @patch.object(app_module, 'primary_vectorizer')
    def test_predict_no_sub_model_returns_null_sub_category(self, mock_vec, mock_model):
        """When no sub-model exists for the predicted category, sub_category is null."""
        mock_vec.transform.return_value = [[0]]
        mock_model.classes_ = np.array(['Bakery'])
        mock_model.predict_proba.return_value = np.array([[0.9]])

        with patch.object(app_module, 'PRIMARY_CONFIDENCE_THRESHOLD', 0.35), \
             patch.dict(app_module.sub_models, {}, clear=True), \
             patch.dict(app_module.sub_vectorizers, {}, clear=True):
            response = self.app.post('/predict', json={'product_name': 'bread'})

        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.json['predicted_primary_category'], 'Bakery')
        self.assertIsNone(response.json['predicted_sub_category'])

    @patch.object(app_module, 'primary_model')
    @patch.object(app_module, 'primary_vectorizer')
    def test_predict_sub_below_threshold_returns_null_sub_category(self, mock_vec, mock_model):
        mock_vec.transform.return_value = [[0]]
        mock_model.classes_ = np.array(['Bakery'])
        mock_model.predict_proba.return_value = np.array([[0.9]])

        sub_mock = _make_sub_mock(['Bread', 'Cakes'], [0.26, 0.74])  # below 35%? No, 0.74 is above
        # Force below threshold
        sub_mock.predict_proba.return_value = np.array([[0.28, 0.28, 0.25, 0.19]])
        sub_mock.classes_ = np.array(['Bread', 'Cakes', 'Pastries', 'Rolls'])

        sub_vec_mock = MagicMock()
        sub_vec_mock.transform.return_value = [[1]]

        with patch.object(app_module, 'PRIMARY_CONFIDENCE_THRESHOLD', 0.35), \
             patch.object(app_module, 'SUB_CONFIDENCE_THRESHOLD', 0.35), \
             patch.dict(app_module.sub_models, {'Bakery': sub_mock}), \
             patch.dict(app_module.sub_vectorizers, {'Bakery': sub_vec_mock}):
            response = self.app.post('/predict', json={'product_name': 'baked good'})

        self.assertEqual(response.status_code, 200)
        self.assertIsNone(response.json['predicted_sub_category'])

    # ------------------------------------------------------------------
    # Happy path — confident prediction with matching sub-model
    # ------------------------------------------------------------------
    @patch.object(app_module, 'primary_model')
    @patch.object(app_module, 'primary_vectorizer')
    def test_predict_successful(self, mock_vec, mock_model):
        mock_vec.transform.return_value = [[0]]
        mock_model.classes_ = np.array(['Fresh___Chilled', 'Bakery'])
        mock_model.predict_proba.return_value = np.array([[0.90, 0.10]])

        sub_mock = MagicMock()
        sub_mock.classes_ = np.array(['Milk', 'Cheese'])
        sub_mock.predict_proba.return_value = np.array([[0.80, 0.20]])

        sub_vec_mock = MagicMock()
        sub_vec_mock.transform.return_value = [[1]]

        with patch.object(app_module, 'PRIMARY_CONFIDENCE_THRESHOLD', 0.35), \
             patch.object(app_module, 'SUB_CONFIDENCE_THRESHOLD', 0.35), \
             patch.dict(app_module.sub_models, {'Fresh___Chilled': sub_mock}), \
             patch.dict(app_module.sub_vectorizers, {'Fresh___Chilled': sub_vec_mock}):
            response = self.app.post('/predict', json={'product_name': 'milk'})

        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.json['predicted_primary_category'], 'Fresh___Chilled')
        self.assertEqual(response.json['predicted_sub_category'], 'Milk')

    # ------------------------------------------------------------------
    # Exception handling
    # ------------------------------------------------------------------
    @patch.object(app_module, 'primary_model')
    @patch.object(app_module, 'primary_vectorizer')
    def test_predict_sub_model_exception_returns_null(self, mock_vec, mock_model):
        mock_vec.transform.return_value = [[0]]
        mock_model.classes_ = np.array(['Bakery'])
        mock_model.predict_proba.return_value = np.array([[0.9]])

        sub_mock = MagicMock()
        sub_mock.predict_proba.side_effect = Exception('sub error')
        sub_vec_mock = MagicMock()
        sub_vec_mock.transform.side_effect = Exception('vec error')

        with patch.object(app_module, 'PRIMARY_CONFIDENCE_THRESHOLD', 0.35), \
             patch.dict(app_module.sub_models, {'Bakery': sub_mock}), \
             patch.dict(app_module.sub_vectorizers, {'Bakery': sub_vec_mock}):
            response = self.app.post('/predict', json={'product_name': 'bread'})

        self.assertEqual(response.status_code, 200)
        self.assertIsNone(response.json['predicted_sub_category'])

    @patch.object(app_module, 'primary_model')
    @patch.object(app_module, 'primary_vectorizer')
    def test_predict_primary_exception(self, mock_vec, mock_model):
        mock_vec.transform.side_effect = Exception('primary error')
        response = self.app.post('/predict', json={'product_name': 'milk'})
        self.assertEqual(response.status_code, 500)
        self.assertIn('error', response.json)

    # ------------------------------------------------------------------
    # clean_text — basic
    # ------------------------------------------------------------------
    def test_clean_text_basic(self):
        self.assertEqual(app_module.clean_text('  Milk!  '), 'milk')
        self.assertEqual(app_module.clean_text('A b c!@#'), 'a b c')
        self.assertEqual(app_module.clean_text(' 123 '), '123')

    # ------------------------------------------------------------------
    # clean_text — quantity/size stripping
    # ------------------------------------------------------------------
    def test_clean_text_strips_metric_units(self):
        self.assertEqual(app_module.clean_text('whole milk 2l'), 'whole milk')
        self.assertEqual(app_module.clean_text('chicken 500g'), 'chicken')
        self.assertEqual(app_module.clean_text('butter 250g'), 'butter')
        self.assertEqual(app_module.clean_text('olive oil 750ml'), 'olive oil')
        self.assertEqual(app_module.clean_text('flour 1.5kg'), 'flour')

    def test_clean_text_strips_pack_patterns(self):
        self.assertEqual(app_module.clean_text('free range eggs 6 pack'), 'free range egg')
        self.assertEqual(app_module.clean_text('pack of 12 bread rolls'), 'bread roll')

    def test_clean_text_strips_multiplier_patterns(self):
        self.assertEqual(app_module.clean_text('yogurt x4'), 'yogurt')

    # ------------------------------------------------------------------
    # clean_text — lemmatization
    # ------------------------------------------------------------------
    def test_clean_text_lemmatizes_standard_plurals(self):
        self.assertEqual(app_module.clean_text('eggs'), 'egg')
        self.assertEqual(app_module.clean_text('bananas'), 'banana')
        self.assertEqual(app_module.clean_text('carrots'), 'carrot')
        self.assertEqual(app_module.clean_text('biscuits'), 'biscuit')

    def test_clean_text_lemmatizes_ies(self):
        self.assertEqual(app_module.clean_text('berries'), 'berry')
        self.assertEqual(app_module.clean_text('strawberries'), 'strawberry')
        self.assertEqual(app_module.clean_text('pastries'), 'pastry')

    def test_clean_text_lemmatizes_oes(self):
        self.assertEqual(app_module.clean_text('tomatoes'), 'tomato')
        self.assertEqual(app_module.clean_text('potatoes'), 'potato')

    def test_clean_text_lemmatizes_ves(self):
        self.assertEqual(app_module.clean_text('loaves'), 'loaf')
        self.assertEqual(app_module.clean_text('halves'), 'half')

    def test_clean_text_does_not_lemmatize_non_plurals(self):
        # Words ending in ss, us, is — must not have 's' stripped
        self.assertEqual(app_module.clean_text('asparagus'), 'asparagus')
        self.assertEqual(app_module.clean_text('hummus'), 'hummus')  # ends in us — skipped
        self.assertEqual(app_module.clean_text('cheese'), 'cheese')  # ends in e, not s

    # ------------------------------------------------------------------
    # _lemmatize_word directly
    # ------------------------------------------------------------------
    def test_lemmatize_word(self):
        lw = app_module._lemmatize_word
        self.assertEqual(lw('eggs'), 'egg')
        self.assertEqual(lw('berries'), 'berry')
        self.assertEqual(lw('loaves'), 'loaf')
        self.assertEqual(lw('tomatoes'), 'tomato')
        self.assertEqual(lw('cheese'), 'cheese')   # ends in 'e', not 's'
        self.assertEqual(lw('grass'), 'grass')     # ends in 'ss' — skip
        self.assertEqual(lw('asparagus'), 'asparagus')  # ends in 'us' — skip
        self.assertEqual(lw('milk'), 'milk')       # no plural ending

    # ------------------------------------------------------------------
    # sanitize_filename
    # ------------------------------------------------------------------
    def test_sanitize_filename(self):
        self.assertEqual(app_module.sanitize_filename('Dairy & Eggs'), 'Dairy_Eggs')
        self.assertEqual(app_module.sanitize_filename('  Home! '), 'Home')
        self.assertEqual(app_module.sanitize_filename(r'A/B\C'), 'A_B_C')


if __name__ == '__main__':
    unittest.main()
