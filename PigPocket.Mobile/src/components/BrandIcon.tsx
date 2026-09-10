import { Image } from 'expo-image';
import type { ImageStyle, StyleProp } from 'react-native';

export type BrandIconName =
  | 'profile'
  | 'notification'
  | 'home'
  | 'help'
  | 'bank'
  | 'security'
  | 'budget'
  | 'subscription'
  | 'activity'
  | 'pig'
  | 'rainy-day'
  | 'alert';

const sources: Record<BrandIconName, number> = {
  profile: require('../../assets/icons/profile.png'),
  notification: require('../../assets/icons/notifications.png'),
  home: require('../../assets/icons/home.png'),
  help: require('../../assets/icons/help.png'),
  bank: require('../../assets/icons/linked-banks.png'),
  security: require('../../assets/icons/security.png'),
  budget: require('../../assets/icons/budgets.png'),
  subscription: require('../../assets/icons/subscriptions.png'),
  activity: require('../../assets/icons/transactions.png'),
  pig: require('../../assets/icons/savings.png'),
  'rainy-day': require('../../assets/icons/rainy-day.png'),
  alert: require('../../assets/icons/spending-alerts.png'),
};

export function BrandIcon({ name, size = 48, style }: { name: BrandIconName; size?: number; style?: StyleProp<ImageStyle> }) {
  return (
    <Image
      source={sources[name]}
      contentFit="contain"
      style={[{ width: size, height: size }, style]}
      accessibilityLabel={`${name.replace('-', ' ')} icon`}
    />
  );
}
