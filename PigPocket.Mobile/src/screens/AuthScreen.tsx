import { Ionicons } from '@expo/vector-icons';
import { useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Image,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  useWindowDimensions,
  View,
} from 'react-native';
import { AuthField } from '../components/AuthField';
import { BrandLogo } from '../components/BrandLogo';
import * as authService from '../services/authService';
import { ApiError } from '../services/httpClient';
import type { AuthMode, AuthResponse } from '../types/auth';

const mascot = require('../../assets/piggy-mascot.png');

function getApiErrorMessage(error: unknown) {
  if (error instanceof ApiError) {
    if (
      typeof error.body === 'object' &&
      error.body !== null &&
      'message' in error.body &&
      typeof error.body.message === 'string'
    ) {
      return error.body.message;
    }

    return 'The server could not complete your request.';
  }

  return 'We could not reach Piggy Pockets. Check the API connection and try again.';
}

export function AuthScreen() {
  const { width } = useWindowDimensions();
  const isDesktop = width >= 820;
  const [mode, setMode] = useState<AuthMode>('login');
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [passwordVisible, setPasswordVisible] = useState(false);
  const [acceptedTerms, setAcceptedTerms] = useState(false);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [authenticatedUser, setAuthenticatedUser] =
    useState<AuthResponse | null>(null);

  const copy = useMemo(
    () =>
      mode === 'login'
        ? {
            heading: 'Welcome back',
            subtitle: "Let's check on your pockets.",
            button: 'Log in',
          }
        : {
            heading: "Let's grow your pocket",
            subtitle: 'Create your Piggy Pockets account.',
            button: 'Create account',
          },
    [mode],
  );

  function switchMode(nextMode: AuthMode) {
    setMode(nextMode);
    setMessage(null);
    setAuthenticatedUser(null);
    setPassword('');
    setPasswordVisible(false);
  }

  async function submit() {
    const normalizedEmail = email.trim().toLowerCase();
    const normalizedName = fullName.trim().replace(/\s+/g, ' ');

    if (mode === 'signup' && !normalizedName) {
      setMessage('Enter your full name.');
      return;
    }

    if (!/^\S+@\S+\.\S+$/.test(normalizedEmail)) {
      setMessage('Enter a valid email address.');
      return;
    }

    if (password.length < 8) {
      setMessage('Password must be at least 8 characters.');
      return;
    }

    if (mode === 'signup' && !acceptedTerms) {
      setMessage('Agree to the Terms and Privacy Policy to continue.');
      return;
    }

    setLoading(true);
    setMessage(null);

    try {
      const response =
        mode === 'login'
          ? await authService.login({ email: normalizedEmail, password })
          : await authService.register({
              firstName: normalizedName.split(' ')[0],
              lastName: normalizedName.split(' ').slice(1).join(' '),
              email: normalizedEmail,
              password,
            });

      setAuthenticatedUser(response);
      setMessage(`Welcome${response.firstName ? `, ${response.firstName}` : ''}!`);
    } catch (error) {
      setMessage(getApiErrorMessage(error));
    } finally {
      setLoading(false);
    }
  }

  return (
    <KeyboardAvoidingView
      style={styles.keyboardView}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <ScrollView
        contentContainerStyle={[
          styles.page,
          isDesktop ? styles.pageDesktop : styles.pageMobile,
        ]}
        keyboardShouldPersistTaps="handled"
      >
        <View
          style={[
            styles.authCard,
            isDesktop ? styles.authCardDesktop : styles.authCardMobile,
            !isDesktop && { width: Math.min(width - 28, 430) },
          ]}
        >
          {isDesktop ? (
            <View style={styles.brandPanel}>
              <BrandLogo light />
              <Image
                source={mascot}
                resizeMode="contain"
                style={styles.desktopMascot}
                accessibilityLabel="Piggy Pockets piggy bank mascot"
              />
              <Text style={styles.slogan}>Small steps.{`\n`}Bigger savings.</Text>
            </View>
          ) : null}

          <View style={[styles.formPanel, !isDesktop && styles.formPanelMobile]}>
            {!isDesktop ? (
              <View style={styles.mobileBrand}>
                <BrandLogo compact />
                <Image
                  source={mascot}
                  resizeMode="contain"
                  style={styles.mobileMascot}
                  accessibilityLabel="Piggy Pockets piggy bank mascot"
                />
              </View>
            ) : null}

            <View style={styles.headingGroup}>
              <Text style={[styles.heading, !isDesktop && styles.headingMobile]}>
                {copy.heading}
              </Text>
              <Text style={[styles.subtitle, !isDesktop && styles.subtitleMobile]}>
                {copy.subtitle}
              </Text>
            </View>

            <View style={styles.fields}>
              {mode === 'signup' ? (
                <AuthField
                  label="Full name"
                  value={fullName}
                  onChangeText={setFullName}
                  placeholder="Your full name"
                  icon="person-outline"
                  autoComplete="name"
                  textContentType="name"
                />
              ) : null}

              <AuthField
                label="Email"
                value={email}
                onChangeText={setEmail}
                placeholder="you@example.com"
                icon="mail-outline"
                autoComplete="email"
                keyboardType="email-address"
                textContentType="emailAddress"
              />

              <AuthField
                label="Password"
                value={password}
                onChangeText={setPassword}
                placeholder="••••••••"
                icon="lock-closed-outline"
                secure
                passwordVisible={passwordVisible}
                onTogglePassword={() => setPasswordVisible((visible) => !visible)}
                autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
                textContentType={mode === 'login' ? 'password' : 'newPassword'}
              />

              {mode === 'signup' ? (
                <>
                  <Text style={styles.passwordHint}>Use at least 8 characters</Text>
                  <Pressable
                    onPress={() => setAcceptedTerms((accepted) => !accepted)}
                    style={styles.termsRow}
                    accessibilityRole="checkbox"
                    accessibilityState={{ checked: acceptedTerms }}
                  >
                    <View style={[styles.checkbox, acceptedTerms && styles.checkboxChecked]}>
                      {acceptedTerms ? (
                        <Ionicons name="checkmark" size={14} color="#fff" />
                      ) : null}
                    </View>
                    <Text style={styles.termsText}>
                      I agree to the{' '}
                      <Text style={styles.link}>Terms and Privacy Policy</Text>
                    </Text>
                  </Pressable>
                </>
              ) : (
                <Pressable
                  onPress={() => setMessage('Password reset is not available yet.')}
                  style={styles.forgotButton}
                >
                  <Text style={styles.link}>Forgot password?</Text>
                </Pressable>
              )}
            </View>

            {message ? (
              <View
                style={[
                  styles.message,
                  authenticatedUser ? styles.successMessage : styles.errorMessage,
                ]}
              >
                <Ionicons
                  name={authenticatedUser ? 'checkmark-circle' : 'information-circle'}
                  size={17}
                  color={authenticatedUser ? '#087044' : '#9b3b31'}
                />
                <Text
                  style={[
                    styles.messageText,
                    authenticatedUser
                      ? styles.successMessageText
                      : styles.errorMessageText,
                  ]}
                >
                  {message}
                </Text>
              </View>
            ) : null}

            <Pressable
              onPress={submit}
              disabled={loading}
              style={({ pressed }) => [
                styles.primaryButton,
                pressed && styles.primaryButtonPressed,
                loading && styles.primaryButtonDisabled,
              ]}
              accessibilityRole="button"
            >
              {loading ? (
                <ActivityIndicator color="#fff" />
              ) : (
                <Text style={styles.primaryButtonText}>{copy.button}</Text>
              )}
            </Pressable>

            <View style={styles.privacyRow}>
              <View style={styles.divider} />
              <Ionicons name="lock-closed" size={17} color="#4f917e" />
              <Text style={styles.privacyText}>Your details stay private.</Text>
              <View style={styles.divider} />
            </View>

            <View style={styles.footerRow}>
              <Text style={styles.footerText}>
                {mode === 'login'
                  ? 'New to Piggy Pockets? '
                  : 'Already have an account? '}
              </Text>
              <Pressable onPress={() => switchMode(mode === 'login' ? 'signup' : 'login')}>
                <Text style={styles.link}>
                  {mode === 'login' ? 'Create account' : 'Log in'}
                </Text>
              </Pressable>
            </View>
          </View>
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  keyboardView: { backgroundColor: '#fbfaf5', flex: 1 },
  page: { alignItems: 'center', flexGrow: 1, justifyContent: 'center' },
  pageDesktop: { padding: 32 },
  pageMobile: { paddingHorizontal: 14, paddingVertical: 18 },
  authCard: {
    backgroundColor: '#fffdf9',
    borderColor: '#e0e0da',
    borderRadius: 18,
    borderWidth: 1,
    overflow: 'hidden',
    shadowColor: '#17382e',
    shadowOffset: { width: 0, height: 8 },
    shadowOpacity: 0.1,
    shadowRadius: 24,
  },
  authCardDesktop: {
    flexDirection: 'row',
    minHeight: 610,
    width: '100%',
    maxWidth: 1030,
  },
  authCardMobile: { maxWidth: 430 },
  brandPanel: {
    alignItems: 'center',
    backgroundColor: '#006447',
    justifyContent: 'space-between',
    paddingBottom: 34,
    paddingHorizontal: 30,
    paddingTop: 52,
    width: '44%',
  },
  desktopMascot: { height: 330, marginHorizontal: -18, width: '112%' },
  slogan: {
    alignSelf: 'flex-start',
    color: '#fffdf6',
    fontSize: 24,
    fontWeight: '700',
    lineHeight: 29,
    marginLeft: 16,
  },
  formPanel: {
    flex: 1,
    justifyContent: 'center',
    paddingHorizontal: 58,
    paddingVertical: 42,
  },
  formPanelMobile: { paddingHorizontal: 18, paddingVertical: 22 },
  mobileBrand: { alignItems: 'center', marginBottom: 5 },
  mobileMascot: { height: 132, marginTop: 4, width: 182 },
  headingGroup: { marginBottom: 28 },
  heading: {
    color: '#003b37',
    fontSize: 32,
    fontWeight: '800',
    letterSpacing: -0.8,
    lineHeight: 39,
  },
  headingMobile: { fontSize: 26, lineHeight: 32, textAlign: 'center' },
  subtitle: {
    color: '#154b75',
    fontSize: 16,
    fontWeight: '400',
    lineHeight: 22,
    marginTop: 2,
  },
  subtitleMobile: { fontSize: 14, lineHeight: 20, textAlign: 'center' },
  fields: { gap: 18 },
  passwordHint: {
    color: '#45677a',
    fontSize: 12,
    fontWeight: '400',
    marginTop: -12,
  },
  forgotButton: { alignSelf: 'flex-end', marginTop: -7 },
  link: { color: '#00884f', fontSize: 14, fontWeight: '600' },
  termsRow: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 9,
    marginTop: -8,
  },
  checkbox: {
    alignItems: 'center',
    backgroundColor: '#fff',
    borderColor: '#246181',
    borderRadius: 4,
    borderWidth: 1.5,
    height: 20,
    justifyContent: 'center',
    width: 20,
  },
  checkboxChecked: { backgroundColor: '#007653', borderColor: '#007653' },
  termsText: {
    color: '#193f52',
    flex: 1,
    fontSize: 12,
    fontWeight: '400',
    lineHeight: 18,
  },
  message: {
    alignItems: 'center',
    borderRadius: 9,
    flexDirection: 'row',
    gap: 8,
    marginTop: 18,
    paddingHorizontal: 12,
    paddingVertical: 10,
  },
  errorMessage: { backgroundColor: '#fff0ed' },
  successMessage: { backgroundColor: '#eaf8f0' },
  messageText: { flex: 1, fontSize: 13, lineHeight: 18 },
  errorMessageText: { color: '#84352d' },
  successMessageText: { color: '#075f3c' },
  primaryButton: {
    alignItems: 'center',
    backgroundColor: '#007653',
    borderRadius: 10,
    justifyContent: 'center',
    marginTop: 24,
    minHeight: 52,
    paddingHorizontal: 20,
  },
  primaryButtonPressed: {
    backgroundColor: '#005f43',
    transform: [{ scale: 0.995 }],
  },
  primaryButtonDisabled: { opacity: 0.7 },
  primaryButtonText: { color: '#fff', fontSize: 16, fontWeight: '700' },
  privacyRow: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 10,
    marginTop: 24,
  },
  divider: { backgroundColor: '#d8dfdc', flex: 1, height: 1 },
  privacyText: { color: '#64839a', fontSize: 12, fontWeight: '400' },
  footerRow: {
    alignItems: 'center',
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'center',
    marginTop: 30,
  },
  footerText: { color: '#123f58', fontSize: 14, fontWeight: '400' },
});
