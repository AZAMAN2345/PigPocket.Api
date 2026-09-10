import Ionicons from '@expo/vector-icons/Ionicons';
import type { ComponentProps, ReactNode } from 'react';
import {
  Image,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  useWindowDimensions,
  View,
} from 'react-native';
import type { AuthResponse } from '../types/auth';
import { BrandIcon, type BrandIconName } from '../components/BrandIcon';

const mascot = require('../../assets/piggy-mascot.png');
type IconName = ComponentProps<typeof Ionicons>['name'];

const colors = {
  ink: '#062653',
  green: '#006244',
  green2: '#09945c',
  mint: '#e6f7ea',
  cream: '#fffdf7',
  border: '#e8e6de',
  muted: '#526e9b',
  coral: '#ff5c77',
  gold: '#ffbd3f',
};

function IconBadge({ name, art, tone = 'green', size = 24 }: { name?: IconName; art?: BrandIconName; tone?: 'green' | 'pink' | 'gold'; size?: number }) {
  const backgroundColor = tone === 'pink' ? '#ffe6ea' : tone === 'gold' ? '#fff4dc' : '#e6f7ea';
  const color = tone === 'pink' ? '#ed315e' : tone === 'gold' ? '#e89b00' : colors.green;
  if (art) return <BrandIcon name={art} size={48} />;
  return <View style={[styles.iconBadge, { backgroundColor }]}><Ionicons name={name!} size={size} color={color} /></View>;
}

function Card({ children, style }: { children: ReactNode; style?: object }) {
  return <View style={[styles.card, style]}>{children}</View>;
}

function SectionTitle({ icon, art, title, subtitle, action }: { icon?: IconName; art?: BrandIconName; title: string; subtitle?: string; action?: string }) {
  return <View style={styles.sectionTitle}>
    {art ? <BrandIcon name={art} size={38} /> : <Ionicons name={icon!} size={27} color={colors.green} />}
    <View style={{ flex: 1 }}>
      <Text style={styles.sectionHeading}>{title}</Text>
      {subtitle ? <Text style={styles.sectionSubtitle}>{subtitle}</Text> : null}
    </View>
    {action ? <Text style={styles.textAction}>{action}</Text> : null}
  </View>;
}

function StatCard({ art, title, amount, subtitle, green = false }: { art: BrandIconName; title: string; amount: string; subtitle: string; green?: boolean }) {
  return <Card style={[styles.statCard, green && styles.mintCard]}>
    <View style={styles.statTop}><IconBadge art={art} /><Ionicons name="chevron-forward" size={22} color={colors.green} /></View>
    <Text style={styles.statTitle}>{title}</Text>
    <Text style={styles.statAmount}>{amount}</Text>
    <Text style={styles.statSubtitle}>{subtitle}</Text>
  </Card>;
}

function BalanceCard() {
  return <View style={styles.balanceCard}>
    <View style={styles.balanceCopy}>
      <View style={styles.balanceTitleRow}><Ionicons name="pie-chart" size={27} color="white" /><Text style={styles.balanceTitle}>Total linked balance</Text></View>
      <Text style={styles.balanceAmount}>₦245,800</Text>
      <Text style={styles.balanceSubtitle}>Across 2 linked accounts</Text>
      <View style={styles.bankRow}>
        <View style={styles.bankChip}><Text style={[styles.bankMark, { color: '#08ab81' }]}>P</Text><Text style={styles.bankText}>OPay</Text></View>
        <View style={styles.bankChip}><Text style={[styles.bankMark, { color: '#e22b35' }]}>Z</Text><Text style={styles.bankText}>Zenith Bank</Text></View>
      </View>
    </View>
    <Image source={mascot} resizeMode="contain" style={styles.balancePig} accessibilityLabel="Piggy Pockets mascot" />
    <Text style={styles.updated}>Updated 2 min ago</Text>
  </View>;
}

function TodayCard() {
  return <Card style={styles.todayCard}>
    <SectionTitle art="subscription" title="Today’s spending" />
    <View style={styles.amountRow}><Text style={styles.todayAmount}>₦6,500</Text><Text style={styles.todayLimit}> / ₦10,000</Text><Text style={styles.percent}>65%</Text></View>
    <View style={styles.progressTrack}><View style={[styles.progressFill, { width: '65%' }]} /></View>
    <Text style={styles.leftText}>₦3,500 left today</Text>
    <View style={styles.warning}><BrandIcon name="alert" size={40} /><View style={{ flex: 1 }}><Text style={styles.warningTitle}>Getting close to your daily limit</Text><Text style={styles.warningCopy}>You’ve used 65% of your daily budget.</Text></View><Ionicons name="chevron-forward" size={22} color="#e92952" /></View>
  </Card>;
}

const chartValues = [18, 29, 39, 25, 34, 47, 31, 51, 36, 48, 70, 57, 82, 60, 71];
function SpendingChart({ mobile }: { mobile: boolean }) {
  const values = mobile ? [31, 20, 38, 32, 68, 45, 33] : chartValues;
  const labels = mobile ? ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'] : ['Sep 1', 'Sep 5', 'Sep 10', 'Sep 15', 'Sep 20', 'Sep 25', 'Sep 30'];
  return <Card style={styles.trendCard}>
    <View style={styles.trendHeader}><SectionTitle art="activity" title="Spending trend" subtitle={`Your daily spending this ${mobile ? 'week' : 'month'}`} /><View style={styles.periodChip}><Text style={styles.periodText}>{mobile ? 'This week' : 'This month'}</Text><Ionicons name="chevron-down" size={16} color={colors.ink} /></View></View>
    <View style={styles.chart}>
      <View style={[styles.gridLine, { top: '18%' }]} /><View style={[styles.gridLine, { top: '48%' }]} /><View style={[styles.gridLine, { top: '78%' }]} />
      <View style={styles.chartBars}>{values.map((value, index) => <View key={index} style={styles.chartColumn}><View style={[styles.chartBar, { height: `${value}%` }]}><View style={styles.chartDot} /></View></View>)}</View>
    </View>
    <View style={styles.chartLabels}>{labels.map(label => <Text key={label} style={styles.chartLabel}>{label}</Text>)}</View>
  </Card>;
}

function BudgetCard() {
  return <Card style={styles.budgetCard}>
    <SectionTitle art="budget" title="Monthly budget" subtitle="₦150,000 total" />
    <View style={styles.budgetBody}>
      <View style={styles.donut}><View style={styles.donutInner}><Text style={styles.donutAmount}>₦62,500</Text><Text style={styles.donutLeft}>left</Text></View></View>
      <View style={styles.legend}>
        {[[colors.coral, 'Food', '40%'], [colors.green2, 'Transport', '20%'], [colors.gold, 'Bills', '20%'], ['#9fdcca', 'Subscriptions', '10%'], ['#bbb8b0', 'Other', '10%']].map(([color, name, value]) => <View key={name} style={styles.legendRow}><View style={[styles.legendDot, { backgroundColor: color }]} /><Text style={styles.legendName}>{name}</Text><Text style={styles.legendValue}>{value}</Text></View>)}
      </View>
    </View>
  </Card>;
}

function SavingsCard() {
  const pockets = [{ icon: 'umbrella' as IconName, name: 'Rainy day', saved: '₦80,000', target: '₦200,000', progress: '40%' }, { icon: 'laptop-outline' as IconName, name: 'New laptop', saved: '₦120,000', target: '₦600,000', progress: '20%' }] as const;
  return <Card style={styles.savingsCard}>
    <SectionTitle art="pig" title="Savings pockets" subtitle="Grow your dreams, one pocket at a time" action="See all" />
    {pockets.map((pocket, index) => <View key={pocket.name} style={[styles.pocketRow, index > 0 && styles.rowDivider]}>{index === 0 ? <BrandIcon name="rainy-day" size={48} /> : <IconBadge name={pocket.icon} tone="gold" />}<View style={{ flex: 1 }}><Text style={styles.pocketName}>{pocket.name}</Text><Text><Text style={styles.pocketSaved}>{pocket.saved}</Text><Text style={styles.pocketTarget}> of {pocket.target}</Text></Text><View style={styles.smallTrack}><View style={[styles.smallFill, { width: pocket.progress }]} /></View></View><Text style={styles.pocketPercent}>{pocket.progress}</Text><Ionicons name="chevron-forward" size={18} color={colors.ink} /></View>)}
    <Pressable style={styles.createPocket}><Ionicons name="add-circle" size={28} color={colors.green} /><Text style={styles.createText}>Create a pocket</Text></Pressable>
  </Card>;
}

function TransactionsCard() {
  const transactions = [
    ['cart', 'Groceries', 'Today, 10:24 AM', '-₦4,500', 'pink'],
    ['bus', 'Transport', 'Today, 8:15 AM', '-₦2,000', 'green'],
    ['wifi', 'Internet', 'Yesterday, 6:32 PM', '-₦15,000', 'green'],
    ['wallet', 'Salary', 'Sep 1, 2026', '+₦250,000', 'green'],
  ] as const;
  return <Card style={styles.transactionsCard}>
    <SectionTitle art="bank" title="Recent transactions" action="View all" />
    {transactions.map(([icon, name, date, amount, tone]) => <View key={name} style={styles.transactionRow}><IconBadge name={icon} tone={tone} size={20} /><View style={{ flex: 1 }}><Text style={styles.transactionName}>{name}</Text><Text style={styles.transactionDate}>{date}</Text></View><Text style={[styles.transactionAmount, amount.startsWith('+') && styles.income]}>{amount}</Text></View>)}
  </Card>;
}

const navigation: readonly [BrandIconName, string][] = [
  ['home', 'Overview'], ['activity', 'Transactions'], ['budget', 'Budgets'],
  ['pig', 'Savings pockets'], ['subscription', 'Subscriptions'], ['security', 'Insights'],
] as const;

function Sidebar({ user, onLogout }: { user: AuthResponse; onLogout: () => void }) {
  return <View style={styles.sidebar}>
    <View>{navigation.map(([icon, label], index) => <Pressable key={label} style={[styles.navItem, index === 0 && styles.navActive]}><BrandIcon name={icon} size={48} /><Text style={[styles.navText, index === 0 && styles.navTextActive]}>{label}</Text></Pressable>)}</View>
    <View>
      <Pressable style={styles.navItem}><BrandIcon name="help" size={48} /><Text style={styles.navText}>Help centre</Text></Pressable>
      <Pressable onPress={onLogout} style={styles.profileRow}><BrandIcon name="profile" size={48} /><View style={{ flex: 1 }}><Text style={styles.profileName}>{`${user.firstName} ${user.lastName}`.trim() || 'Piggy Saver'}</Text><Text style={styles.handle}>Log out</Text></View><Ionicons name="chevron-forward" size={20} color={colors.ink} /></Pressable>
    </View>
  </View>;
}

function MobileNav({ onLogout }: { onLogout: () => void }) {
  const items: readonly [BrandIconName, string][] = [['home', 'Home'], ['activity', 'Activity'], ['budget', 'Budgets'], ['pig', 'Savings'], ['profile', 'Profile']];
  return <View style={styles.mobileNav}>{items.map(([icon, label], index) => <Pressable key={label} onPress={index === 4 ? onLogout : undefined} style={[styles.mobileNavItem, index === 0 && styles.mobileNavActive]}><BrandIcon name={icon} size={38} /><Text style={[styles.mobileNavText, index === 0 && { color: colors.green, fontWeight: '700' }]}>{label}</Text></Pressable>)}</View>;
}

export function DashboardScreen({ user, loading, message, onLogout }: { user: AuthResponse; loading: boolean; message: string | null; onLogout: () => void }) {
  const { width } = useWindowDimensions();
  const desktop = width >= 980;
  const firstName = user.firstName || 'Saver';
  return <View style={styles.screen}>
    {desktop ? <Sidebar user={user} onLogout={onLogout} /> : null}
    <View style={styles.contentShell}>
      <ScrollView contentContainerStyle={[styles.content, !desktop && styles.mobileContent]} showsVerticalScrollIndicator={false}>
        <View style={styles.header}>
          <View><Text style={[styles.welcome, !desktop && styles.mobileWelcome]}>Welcome back, {firstName} 👋</Text>{!desktop ? <Text style={styles.mobileTagline}>🍃  Your money, at a glance</Text> : null}</View>
          <View style={styles.headerActions}>{desktop ? <View style={styles.dateButton}><Ionicons name="calendar-outline" size={22} color={colors.green} /><Text style={styles.dateText}>September 2026</Text><Ionicons name="chevron-down" size={16} color={colors.ink} /></View> : null}<BrandIcon name="notification" size={43} />{desktop ? <Pressable style={styles.linkBank}><Ionicons name="link" size={22} color="white" /><Text style={styles.linkBankText}>Link bank</Text></Pressable> : null}</View>
        </View>
        {message ? <View style={styles.dashboardMessage}><Text style={styles.dashboardMessageText}>{message}</Text></View> : null}
        <View style={[styles.topGrid, !desktop && styles.mobileStack]}><BalanceCard /><View style={styles.statsGrid}><StatCard art="budget" title="Spent this month" amount="₦87,500" subtitle="Across all categories" /><StatCard art="rainy-day" title="Total saved" amount="₦200,000" subtitle="You’re building a brighter tomorrow!" green /></View></View>
        {!desktop ? <TodayCard /> : null}
        <View style={[styles.middleGrid, !desktop && styles.mobileStack]}><SpendingChart mobile={!desktop} />{desktop ? <TodayCard /> : null}</View>
        <View style={[styles.bottomGrid, !desktop && styles.mobileStack]}><BudgetCard /><SavingsCard /><TransactionsCard /></View>
      </ScrollView>
      {!desktop ? <MobileNav onLogout={onLogout} /> : null}
      {loading ? <View style={styles.loadingOverlay}><Text style={styles.loadingText}>Signing out…</Text></View> : null}
    </View>
  </View>;
}

const styles = StyleSheet.create({
  screen: { flex: 1, flexDirection: 'row', backgroundColor: colors.cream }, contentShell: { flex: 1 }, content: { padding: 22, gap: 16 }, mobileContent: { padding: 18, paddingBottom: 112, maxWidth: 780, width: '100%', alignSelf: 'center' },
  sidebar: { width: 238, paddingHorizontal: 16, paddingVertical: 32, borderRightWidth: 1, borderRightColor: colors.border, justifyContent: 'space-between', backgroundColor: '#fffefa' }, navItem: { minHeight: 66, borderRadius: 16, paddingHorizontal: 12, flexDirection: 'row', alignItems: 'center', gap: 12 }, navActive: { backgroundColor: '#e5f5e7' }, navText: { color: colors.ink, fontSize: 16 }, navTextActive: { color: '#003f32', fontWeight: '700' },
  iconBadge: { width: 48, height: 48, borderRadius: 14, justifyContent: 'center', alignItems: 'center' }, profileRow: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingHorizontal: 8, paddingTop: 20, marginTop: 12, borderTopWidth: 1, borderTopColor: colors.border }, avatar: { width: 48, height: 48, borderRadius: 24, backgroundColor: colors.mint }, profileName: { color: colors.ink, fontSize: 14, fontWeight: '700' }, handle: { color: colors.muted, fontSize: 12, marginTop: 2 },
  header: { minHeight: 54, flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }, welcome: { color: colors.ink, fontSize: 24, fontWeight: '500' }, mobileWelcome: { fontSize: 31, fontWeight: '800', color: '#003e3a' }, mobileTagline: { color: colors.muted, fontSize: 18, marginTop: 2 }, headerActions: { flexDirection: 'row', alignItems: 'center', gap: 25 }, dateButton: { flexDirection: 'row', alignItems: 'center', gap: 10, borderRightWidth: 1, borderRightColor: colors.border, paddingRight: 24 }, dateText: { color: colors.ink, fontSize: 15 }, notificationDot: { position: 'absolute', right: -1, top: -2, width: 13, height: 13, borderRadius: 8, backgroundColor: '#f8004f', borderWidth: 2, borderColor: colors.cream }, linkBank: { flexDirection: 'row', alignItems: 'center', gap: 10, minHeight: 48, paddingHorizontal: 24, backgroundColor: colors.green, borderRadius: 15 }, linkBankText: { color: 'white', fontSize: 18, fontWeight: '700' },
  card: { backgroundColor: '#fff', borderRadius: 16, borderWidth: 1, borderColor: colors.border, padding: 18 }, topGrid: { flexDirection: 'row', gap: 14 }, mobileStack: { flexDirection: 'column' }, balanceCard: { minHeight: 216, flex: 1.55, borderRadius: 16, backgroundColor: colors.green, overflow: 'hidden', padding: 25 }, balanceCopy: { zIndex: 2 }, balanceTitleRow: { flexDirection: 'row', alignItems: 'center', gap: 12 }, balanceTitle: { color: 'white', fontSize: 20 }, balanceAmount: { color: 'white', fontSize: 50, lineHeight: 61, fontWeight: '900' }, balanceSubtitle: { color: '#eefcf5', fontSize: 16 }, bankRow: { flexDirection: 'row', gap: 10, marginTop: 14 }, bankChip: { backgroundColor: 'white', borderRadius: 9, paddingHorizontal: 13, minHeight: 39, flexDirection: 'row', alignItems: 'center', gap: 8 }, bankMark: { fontSize: 25, fontWeight: '900', fontStyle: 'italic' }, bankText: { color: colors.ink, fontWeight: '600' }, balancePig: { position: 'absolute', right: 6, top: 22, width: '42%', height: '76%' }, updated: { position: 'absolute', right: 18, bottom: 15, color: 'white', fontSize: 12 }, statsGrid: { flex: 1.8, flexDirection: 'row', gap: 12 }, statCard: { flex: 1, justifyContent: 'center' }, mintCard: { backgroundColor: '#eaf9ee' }, statTop: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }, statTitle: { color: colors.ink, fontSize: 16, fontWeight: '700', marginTop: 7 }, statAmount: { color: '#003f36', fontSize: 36, fontWeight: '900', marginTop: 5 }, statSubtitle: { color: colors.muted, marginTop: 3 },
  middleGrid: { flexDirection: 'row', gap: 14 }, trendCard: { flex: 1.7, minHeight: 250 }, todayCard: { flex: 1, minHeight: 250 }, sectionTitle: { flexDirection: 'row', alignItems: 'center', gap: 10 }, sectionHeading: { color: colors.ink, fontSize: 18, fontWeight: '800' }, sectionSubtitle: { color: colors.muted, fontSize: 13, marginTop: 1 }, textAction: { color: colors.green, fontSize: 13, fontWeight: '700' }, trendHeader: { flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'space-between' }, periodChip: { flexDirection: 'row', alignItems: 'center', gap: 8, paddingHorizontal: 14, minHeight: 38, borderRadius: 20, borderWidth: 1, borderColor: '#dcdedc' }, periodText: { color: colors.ink, fontSize: 13 }, chart: { height: 125, marginTop: 15, borderWidth: 1, borderColor: '#e7eee9', overflow: 'hidden' }, gridLine: { position: 'absolute', height: 1, width: '100%', backgroundColor: '#e6eee8' }, chartBars: { height: '100%', paddingHorizontal: 5, flexDirection: 'row', alignItems: 'flex-end' }, chartColumn: { flex: 1, height: '100%', justifyContent: 'flex-end', alignItems: 'center' }, chartBar: { width: '75%', minHeight: 7, maxWidth: 38, backgroundColor: '#d8f2df', borderTopWidth: 3, borderTopColor: colors.green2, borderTopLeftRadius: 8, borderTopRightRadius: 8 }, chartDot: { width: 8, height: 8, borderRadius: 5, backgroundColor: colors.green2, position: 'absolute', top: -5, alignSelf: 'center', borderWidth: 1, borderColor: 'white' }, chartLabels: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 7 }, chartLabel: { color: colors.muted, fontSize: 10 },
  amountRow: { flexDirection: 'row', alignItems: 'baseline', marginTop: 12 }, todayAmount: { color: '#003f36', fontSize: 31, fontWeight: '900' }, todayLimit: { color: colors.ink, fontSize: 18 }, percent: { marginLeft: 'auto', color: colors.ink, fontSize: 16 }, progressTrack: { height: 13, backgroundColor: '#e5e5e5', borderRadius: 8, overflow: 'hidden', marginTop: 8 }, progressFill: { height: '100%', backgroundColor: colors.green2, borderRadius: 8 }, leftText: { color: colors.muted, marginTop: 7, fontSize: 14 }, warning: { backgroundColor: '#fff0f2', borderRadius: 12, padding: 10, flexDirection: 'row', alignItems: 'center', gap: 9, marginTop: 14 }, warningIcon: { width: 35, height: 35, borderRadius: 11, backgroundColor: colors.green, alignItems: 'center', justifyContent: 'center' }, warningBang: { width: 22, height: 22, borderRadius: 12, textAlign: 'center', color: 'white', backgroundColor: '#ff3c62', fontWeight: '900' }, warningTitle: { color: '#e92952', fontWeight: '700', fontSize: 12 }, warningCopy: { color: '#d74b67', fontSize: 11, marginTop: 2 },
  bottomGrid: { flexDirection: 'row', gap: 12 }, budgetCard: { flex: 1.1 }, savingsCard: { flex: 1.2 }, transactionsCard: { flex: 1.15 }, budgetBody: { flexDirection: 'row', alignItems: 'center', gap: 16, marginTop: 18 }, donut: { width: 140, height: 140, borderRadius: 70, borderWidth: 27, borderTopColor: colors.coral, borderRightColor: colors.coral, borderBottomColor: colors.green2, borderLeftColor: colors.gold, alignItems: 'center', justifyContent: 'center' }, donutInner: { alignItems: 'center' }, donutAmount: { color: '#003f36', fontSize: 19, fontWeight: '900' }, donutLeft: { color: colors.ink, fontSize: 13 }, legend: { flex: 1, gap: 10 }, legendRow: { flexDirection: 'row', alignItems: 'center', gap: 8 }, legendDot: { width: 18, height: 18, borderRadius: 9 }, legendName: { flex: 1, color: colors.ink, fontSize: 12 }, legendValue: { color: colors.muted, fontSize: 12 },
  pocketRow: { minHeight: 68, flexDirection: 'row', gap: 10, alignItems: 'center', paddingVertical: 8 }, rowDivider: { borderTopWidth: 1, borderTopColor: colors.border }, pocketName: { color: colors.ink, fontSize: 13 }, pocketSaved: { color: colors.ink, fontSize: 13, fontWeight: '800' }, pocketTarget: { color: colors.muted, fontSize: 11 }, smallTrack: { backgroundColor: '#e5e6e6', height: 8, borderRadius: 6, marginTop: 4, overflow: 'hidden' }, smallFill: { backgroundColor: colors.green2, height: '100%', borderRadius: 6 }, pocketPercent: { color: colors.muted, fontSize: 11 }, createPocket: { marginTop: 7, minHeight: 41, backgroundColor: '#def4df', borderRadius: 10, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8 }, createText: { color: '#003f36', fontWeight: '700' },
  transactionRow: { minHeight: 58, flexDirection: 'row', alignItems: 'center', gap: 10, borderTopWidth: 1, borderTopColor: colors.border }, transactionName: { color: colors.ink, fontSize: 13 }, transactionDate: { color: colors.muted, fontSize: 11, marginTop: 2 }, transactionAmount: { color: colors.ink, fontWeight: '800', fontSize: 13 }, income: { color: colors.green2 }, mobileNav: { position: 'absolute', left: 0, right: 0, bottom: 0, minHeight: 84, backgroundColor: 'rgba(255,255,251,0.97)', borderTopWidth: 1, borderTopColor: colors.border, flexDirection: 'row', justifyContent: 'space-around', paddingHorizontal: 8, paddingVertical: 7 }, mobileNavItem: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 3, borderRadius: 18 }, mobileNavActive: { backgroundColor: '#e4f6e7' }, mobileNavText: { color: colors.muted, fontSize: 11 }, dashboardMessage: { padding: 12, borderRadius: 10, backgroundColor: '#fff0ed' }, dashboardMessageText: { color: '#84352d' }, loadingOverlay: { position: 'absolute', inset: 0, backgroundColor: 'rgba(255,255,255,0.75)', alignItems: 'center', justifyContent: 'center' }, loadingText: { color: colors.green, fontWeight: '700' },
});
