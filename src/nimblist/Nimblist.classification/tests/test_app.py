import unittest
import sys
import os
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from app import app
import json
from unittest.mock import patch, MagicMock
import app as app_module

class TestApp(unittest.TestCase):
    def setUp(self):
        self.app = app.test_client()
        self.app.testing = True

    def test_health_check(self):
        """Test the health check endpoint"""
        response = self.app.get('/health')
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.json, {'status': 'ok'})
        
    def test_predict_endpoint_exists(self):
        """Test that predict endpoint exists"""
        # Just testing that the endpoint is available
        response = self.app.post('/predict', 
                                json={'item': 'test item'})
        # We don't care about the response code, just that it's not a 404
        self.assertNotEqual(response.status_code, 404)

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

    def test_predict_models_not_loaded(self):
        with patch.object(app_module, 'primary_model', None), patch.object(app_module, 'primary_vectorizer', None):
            response = self.app.post('/predict', json={'product_name': 'milk'})
            self.assertEqual(response.status_code, 500)
            self.assertIn('error', response.json)

    @patch.object(app_module, 'primary_model')
    @patch.object(app_module, 'primary_vectorizer')
    def test_predict_primary_unknown(self, mock_vectorizer, mock_model):
        mock_vectorizer.transform.return_value = [[0]]
        mock_model.predict.return_value = ['Unknown']
        response = self.app.post('/predict', json={'product_name': 'unknown item'})
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.json['predicted_primary_category'], 'Unknown')
        self.assertEqual(response.json['predicted_sub_category'], 'N/A')

    @patch.object(app_module, 'primary_model')
    @patch.object(app_module, 'primary_vectorizer')
    def test_predict_no_sub_model(self, mock_vectorizer, mock_model):
        mock_vectorizer.transform.return_value = [[0]]
        mock_model.predict.return_value = ['Dairy']
        # Remove sub-models for this test
        with patch.dict(app_module.sub_models, {}, clear=True), patch.dict(app_module.sub_vectorizers, {}, clear=True):
            response = self.app.post('/predict', json={'product_name': 'milk'})
            self.assertEqual(response.status_code, 200)
            self.assertEqual(response.json['predicted_primary_category'], 'Dairy')
            self.assertEqual(response.json['predicted_sub_category'], 'No Sub-Model')

    @patch.object(app_module, 'primary_model')
    @patch.object(app_module, 'primary_vectorizer')
    def test_predict_successful(self, mock_vectorizer, mock_model):
        mock_vectorizer.transform.return_value = [[0]]
        mock_model.predict.return_value = ['Dairy']
        # Patch sub-models and vectorizers
        with patch.dict(app_module.sub_models, {'Dairy': MagicMock()}), \
             patch.dict(app_module.sub_vectorizers, {'Dairy': MagicMock()}):
            app_module.sub_vectorizers['Dairy'].transform.return_value = [[1]]
            app_module.sub_models['Dairy'].predict.return_value = ['Milk']
            response = self.app.post('/predict', json={'product_name': 'milk'})
            self.assertEqual(response.status_code, 200)
            self.assertEqual(response.json['predicted_primary_category'], 'Dairy')
            self.assertEqual(response.json['predicted_sub_category'], 'Milk')

    @patch.object(app_module, 'primary_model')
    @patch.object(app_module, 'primary_vectorizer')
    def test_predict_sub_model_exception(self, mock_vectorizer, mock_model):
        mock_vectorizer.transform.return_value = [[0]]
        mock_model.predict.return_value = ['Dairy']
        with patch.dict(app_module.sub_models, {'Dairy': MagicMock()}), \
             patch.dict(app_module.sub_vectorizers, {'Dairy': MagicMock()}):
            app_module.sub_vectorizers['Dairy'].transform.side_effect = Exception('sub error')
            response = self.app.post('/predict', json={'product_name': 'milk'})
            self.assertEqual(response.status_code, 200)
            self.assertEqual(response.json['predicted_sub_category'], 'Prediction Error')

    @patch.object(app_module, 'primary_model')
    @patch.object(app_module, 'primary_vectorizer')
    def test_predict_primary_exception(self, mock_vectorizer, mock_model):
        mock_vectorizer.transform.side_effect = Exception('primary error')
        response = self.app.post('/predict', json={'product_name': 'milk'})
        self.assertEqual(response.status_code, 500)
        self.assertIn('error', response.json)

    def test_clean_text(self):
        self.assertEqual(app_module.clean_text('  Milk!  '), 'milk')
        self.assertEqual(app_module.clean_text('A b c!@#'), 'a b c')
        self.assertEqual(app_module.clean_text(' 123 '), '123')

    def test_sanitize_filename(self):
        self.assertEqual(app_module.sanitize_filename('Dairy & Eggs'), 'Dairy_Eggs')
        self.assertEqual(app_module.sanitize_filename('  Home! '), 'Home')
        self.assertEqual(app_module.sanitize_filename('A/B\C'), 'A_B_C')

if __name__ == '__main__':
    unittest.main()
