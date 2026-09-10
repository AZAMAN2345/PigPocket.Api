# Pig Pocket Mobile

React Native client scaffolded with Expo SDK 57 and TypeScript.

## Start developing

```powershell
cd PigPocket.Mobile
npm start
```

Scan the QR code with Expo Go on a physical device, or press `w` for web. Android emulator support requires Android Studio/SDK and `adb` on `PATH`.

## API configuration

The backend's local HTTP profile runs at `http://localhost:5022`.

Copy `.env.example` to `.env.local` and set `EXPO_PUBLIC_API_URL` when needed:

```dotenv
EXPO_PUBLIC_API_URL=http://192.168.1.20:5022
```

- Web and iOS Simulator: `http://localhost:5022`
- Android Emulator: `http://10.0.2.2:5022`
- Physical device: use the development computer's LAN IP and make sure the API listens on that interface

Never place secrets in an `EXPO_PUBLIC_` variable; Expo includes these values in the client bundle.

## Useful commands

```powershell
npm run typecheck
npm run doctor
npm run android
npm run ios
npm run web
```

Local iOS native builds require macOS. Expo Go can still run the project on an iPhone without a local Xcode installation.
