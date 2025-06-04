import unittest
import sys
import os
import json
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from app import app

class TestPredictionApi(unittest.TestCase):
    def setUp(self):
        self.app = app.test_client()
        self.app.testing = True

    def test_predict_error_handling(self):
        """Test error handling in predict endpoint"""
        # Test missing product_name
        response = self.app.post('/predict', 
                                json={'item': 'missing product_name'})
        self.assertIn('error', response.json)
        
        # Test empty request
        response = self.app.post('/predict', 
                                json={})
        self.assertIn('error', response.json)
        
        # Test non-JSON request
        response = self.app.post('/predict', 
                                data='not json')
        self.assertEqual(response.status_code, 400)

if __name__ == '__main__':
    unittest.main()
