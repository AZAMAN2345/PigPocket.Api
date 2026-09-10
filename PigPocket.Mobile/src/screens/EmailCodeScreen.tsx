import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Image, Pressable, ScrollView, StyleSheet, Text, TextInput, useWindowDimensions, View } from 'react-native';
import { BrandLogo } from '../components/BrandLogo';
import { AuthField } from '../components/AuthField';
import * as auth from '../services/authService';
import { ApiError } from '../services/httpClient';
import type { AuthResponse } from '../types/auth';

export function EmailCodeScreen({ email, reset, onBack, onAuthenticated, onReset }: {
  email: string; reset: boolean; onBack: () => void; onAuthenticated: (user: AuthResponse) => void; onReset: () => void;
}) {
  const desktop = useWindowDimensions().width >= 820;
  const [cells, setCells] = useState<string[]>(Array(6).fill(''));
  const code = cells.join('');
  const [password, setPassword] = useState('');
  const [visible, setVisible] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [remaining, setRemaining] = useState(45);
  const inputs = useRef<(TextInput | null)[]>([]);
  useEffect(() => { const timer = setInterval(() => setRemaining(n => Math.max(0, n - 1)), 1000); return () => clearInterval(timer); }, []);
  const [local, domain] = email.split('@');
  const masked = `${local.slice(0, 2)}***@${domain}`;
  async function run(resend = false) {
    if (busy) return;
    if (!resend && code.length !== 6) { setMessage('Enter all 6 digits.'); return; }
    if (!resend && reset && (password.length < 8 || password.length > 128)) { setMessage('Use a password between 8 and 128 characters.'); return; }
    setBusy(true); setMessage('');
    try {
      if (resend) { await auth.sendCode(email, reset); setRemaining(45); setCells(Array(6).fill('')); setMessage('If your account is eligible, a new code has been sent.'); inputs.current[0]?.focus(); }
      else if (reset) { await auth.resetPassword(email, code, password); onReset(); }
      else onAuthenticated(await auth.verifyEmail(email, code));
    } catch (error) {
      setMessage(error instanceof ApiError && error.body && typeof error.body === 'object' && 'message' in error.body ? String(error.body.message) : 'Unable to complete your request. Please try again.');
    } finally { setBusy(false); }
  }
  return <ScrollView contentContainerStyle={s.page} keyboardShouldPersistTaps="handled">
    <View style={[s.card, desktop && s.desktop]}>
      {desktop && <View style={s.brand}><BrandLogo light /><Image source={require('../../assets/piggy-mascot.png')} style={s.bigPig} resizeMode="contain" /><Text style={s.slogan}>One step <Text style={{ color: '#86ce67' }}>closer.</Text></Text></View>}
      <View style={[s.form, desktop && { padding: 48 }]}>
        {!desktop && <View style={s.mobileBrand}><BrandLogo compact /><Image source={require('../../assets/piggy-mascot.png')} style={s.smallPig} resizeMode="contain" /></View>}
        <Pressable disabled={busy} onPress={onBack} accessibilityRole="button"><Text style={s.back}>?  Back to {reset ? 'log in' : 'sign up'}</Text></Pressable>
        <Text style={[s.title, desktop && { fontSize: 42 }]}>{reset ? 'Reset your password' : 'Check your email'}</Text>
        <Text style={s.subtitle}>Enter the 6-digit code we sent to{`\n`}<Text style={{ fontWeight: '700' }}>{masked}</Text></Text>
        <View style={s.codes}>{Array.from({ length: 6 }, (_, index) => <TextInput key={index}
          ref={ref => { inputs.current[index] = ref; }} value={cells[index]} editable={!busy}
          accessibilityLabel={`Code digit ${index + 1}`} keyboardType="number-pad" autoComplete={index === 0 ? 'one-time-code' : 'off'}
          textContentType={index === 0 ? 'oneTimeCode' : 'none'} selectTextOnFocus maxLength={6}
          onChangeText={value => {
            const digits = value.replace(/\D/g, '');
            if (digits.length > 1) { setCells(Array.from({ length: 6 }, (_, i) => digits[i] ?? '')); inputs.current[Math.min(digits.length, 5)]?.focus(); }
            else { setCells(previous => previous.map((digit, i) => i === index ? digits : digit)); if (digits) inputs.current[Math.min(index + 1, 5)]?.focus(); }
          }}
          onKeyPress={({ nativeEvent }) => { if (nativeEvent.key === 'Backspace' && !cells[index] && index > 0) inputs.current[index - 1]?.focus(); }}
          style={[s.digit, desktop && { height: 70 }, code.length === index && s.active]} />)}</View>
        {reset && <AuthField label="New password" value={password} onChangeText={setPassword} placeholder="At least 8 characters" icon="lock-closed-outline" secure passwordVisible={visible} onTogglePassword={() => setVisible(v => !v)} autoComplete="new-password" />}
        {!!message && <Text accessibilityRole="alert" style={s.message}>{message}</Text>}
        <Pressable onPress={() => run()} disabled={busy} accessibilityRole="button" style={[s.button, busy && { opacity: 0.65 }]}>{busy ? <ActivityIndicator color="white" /> : <Text style={s.buttonText}>{reset ? 'Change password' : 'Verify email'}</Text>}</Pressable>
        <Text style={s.expiry}>Code expires in 10 minutes</Text>
        <View style={s.footer}><Text style={s.footerText}>Didn’t get a code?</Text><Pressable accessibilityRole="button" disabled={remaining > 0 || busy} onPress={() => run(true)}><Text style={[s.link, remaining > 0 && { color: '#777' }]}>{remaining > 0 ? `Resend in 00:${String(remaining).padStart(2, '0')}` : 'Resend code'}</Text></Pressable></View>
        <Pressable disabled={busy} onPress={onBack} accessibilityRole="button"><Text style={[s.link, { textAlign: 'center', marginTop: 24, textDecorationLine: 'underline' }]}>Change email</Text></Pressable>
      </View>
    </View>
  </ScrollView>;
}
const s = StyleSheet.create({
  page: { flexGrow: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: '#fbf8f0', padding: 16 },
  card: { width: '100%', maxWidth: 440, borderWidth: 1, borderColor: '#d9d9ce', borderRadius: 22, overflow: 'hidden', backgroundColor: '#fffcf5' },
  desktop: { maxWidth: 1100, flexDirection: 'row', minHeight: 690 },
  brand: { width: '39%', backgroundColor: '#00503c', padding: 34, alignItems: 'center', justifyContent: 'space-around' },
  bigPig: { width: '115%', height: 350 }, smallPig: { width: 210, height: 190 }, mobileBrand: { alignItems: 'center' },
  slogan: { color: '#fffaf0', fontWeight: '800', fontSize: 28 }, form: { flex: 1, padding: 22, justifyContent: 'center' },
  back: { color: '#00503c', fontSize: 16, marginBottom: 30 }, title: { color: '#003e30', fontSize: 29, fontWeight: '900', textAlign: 'center', letterSpacing: -1 },
  subtitle: { color: '#343c43', textAlign: 'center', fontSize: 16, lineHeight: 25, marginTop: 14 },
  codes: { flexDirection: 'row', gap: 8, marginTop: 34, marginBottom: 20 }, digit: { flex: 1, minWidth: 0, height: 54, borderWidth: 1.5, borderColor: '#d8d6ce', borderRadius: 12, textAlign: 'center', fontSize: 25, color: '#00503c' }, active: { borderColor: '#00503c', borderWidth: 2 },
  button: { marginTop: 18, minHeight: 54, backgroundColor: '#00543e', borderRadius: 12, alignItems: 'center', justifyContent: 'center' }, buttonText: { color: '#fff', fontSize: 19, fontWeight: '700' },
  expiry: { color: '#686c70', textAlign: 'center', marginTop: 16 }, footer: { borderTopWidth: 1, borderTopColor: '#e1dfd6', paddingTop: 26, marginTop: 32, flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'center', gap: 10 }, footerText: { color: '#343c43' }, link: { color: '#00543e', fontSize: 15 }, message: { color: '#923c30', marginTop: 12, lineHeight: 21 },
});

