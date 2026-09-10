import { Ionicons } from '@expo/vector-icons';
import type { ComponentProps } from 'react';
import { Pressable, StyleSheet, Text, TextInput, View } from 'react-native';

type IconName = ComponentProps<typeof Ionicons>['name'];

type AuthFieldProps = {
  label: string;
  value: string;
  placeholder: string;
  icon: IconName;
  secure?: boolean;
  passwordVisible?: boolean;
  onTogglePassword?: () => void;
  onChangeText: (value: string) => void;
  autoComplete?: ComponentProps<typeof TextInput>['autoComplete'];
  keyboardType?: ComponentProps<typeof TextInput>['keyboardType'];
  textContentType?: ComponentProps<typeof TextInput>['textContentType'];
};

export function AuthField({
  label,
  value,
  placeholder,
  icon,
  secure = false,
  passwordVisible = false,
  onTogglePassword,
  onChangeText,
  autoComplete,
  keyboardType,
  textContentType,
}: AuthFieldProps) {
  return (
    <View style={styles.field}>
      <Text style={styles.label}>{label}</Text>
      <View style={styles.inputShell}>
        <Ionicons name={icon} size={19} color="#135c83" />
        <TextInput
          value={value}
          onChangeText={onChangeText}
          placeholder={placeholder}
          placeholderTextColor="#8198ba"
          autoCapitalize="none"
          autoCorrect={false}
          autoComplete={autoComplete}
          keyboardType={keyboardType}
          textContentType={textContentType}
          secureTextEntry={secure && !passwordVisible}
          style={styles.input}
          accessibilityLabel={label}
        />
        {secure && onTogglePassword ? (
          <Pressable
            onPress={onTogglePassword}
            hitSlop={10}
            accessibilityRole="button"
            accessibilityLabel={passwordVisible ? 'Hide password' : 'Show password'}
          >
            <Ionicons
              name={passwordVisible ? 'eye-off-outline' : 'eye-outline'}
              size={20}
              color="#135c83"
            />
          </Pressable>
        ) : null}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  field: { gap: 8 },
  label: { color: '#062f36', fontSize: 14, fontWeight: '600' },
  inputShell: {
    alignItems: 'center',
    backgroundColor: '#ffffff',
    borderColor: '#c9d4df',
    borderRadius: 10,
    borderWidth: 1,
    flexDirection: 'row',
    minHeight: 50,
    paddingHorizontal: 14,
  },
  input: {
    color: '#082f36',
    flex: 1,
    fontSize: 16,
    fontWeight: '400',
    paddingHorizontal: 12,
    paddingVertical: 10,
  },
});
