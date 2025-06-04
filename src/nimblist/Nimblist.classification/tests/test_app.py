import unittest
import sys
import os
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from app import app

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

if __name__ == '__main__':
    unittest.main()
