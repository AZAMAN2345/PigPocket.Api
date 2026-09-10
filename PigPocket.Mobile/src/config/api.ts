import { Platform } from 'react-native';

const configuredApiUrl = process.env.EXPO_PUBLIC_API_URL?.trim();

export const API_BASE_URL = (
  configuredApiUrl ||
  (Platform.OS === 'android'
    ? 'http://10.0.2.2:5022'
    : 'http://localhost:5022')
).replace(/\/$/, '');
