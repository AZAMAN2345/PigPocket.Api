import Ionicons from '@expo/vector-icons/Ionicons';
import { StyleSheet, Text, View } from 'react-native';

type BrandLogoProps = {
  light?: boolean;
  compact?: boolean;
};

export function BrandLogo({ light = false, compact = false }: BrandLogoProps) {
  return (
    <View style={styles.container} accessibilityLabel="Piggy Pockets">
      <View>
        <Ionicons
          name="leaf"
          color="#6cc44a"
          size={compact ? 15 : 22}
          style={[styles.leaf, compact && styles.leafCompact]}
        />
        <Text
          style={[
            styles.wordmark,
            { color: light ? '#fffdf5' : '#063e32' },
            compact && styles.wordmarkCompact,
          ]}
        >
          Piggy <Text style={styles.pockets}>Pockets</Text>
        </Text>
      </View>
      <Text
        style={[
          styles.tagline,
          { color: light ? '#e5f3ed' : '#245f52' },
          compact && styles.taglineCompact,
        ]}
      >
        Your money, growing with you.
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { alignItems: 'center' },
  wordmark: {
    fontSize: 36,
    fontWeight: '800',
    letterSpacing: -1.5,
    lineHeight: 42,
  },
  wordmarkCompact: { fontSize: 27, lineHeight: 32 },
  pockets: { color: '#64c74d' },
  leaf: {
    left: 44,
    position: 'absolute',
    top: -15,
    transform: [{ rotate: '-25deg' }],
  },
  leafCompact: { left: 32, top: -10 },
  tagline: { fontSize: 15, fontWeight: '400', marginTop: -1 },
  taglineCompact: { fontSize: 10, marginTop: -2 },
});
