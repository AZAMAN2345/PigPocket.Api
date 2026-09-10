using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PigPocket.Api.DTOs.Auth;
using PigPocket.Api.Models;
using PigPocket.Api.Services;

BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
var client = new MongoClient("mongodb://localhost:27017/?serverSelectionTimeoutMS=5000");
var name = "PigPocket_AuthTest_" + Guid.NewGuid().ToString("N");
var db = client.GetDatabase(name);
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
 ["Jwt:Key"] = "integration-test-only-key-64-characters-long-and-not-for-production", ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test", ["Jwt:ExpiresInMinutes"] = "15", ["Resend:ApiKey"] = "test"
}).Build();
var mail = new FakeMail();
var service = new AuthService(db, new JwtService(config), new AuthEmailService(new HttpClient(mail), config, NullLogger<AuthEmailService>.Instance), config);
var users = db.GetCollection<User>("Users");
const string email = "auth-flow@example.com", password = "Strong-test-password1!";
static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
static async Task Reject(Func<Task> action, string code) {
 try { await action(); } catch (AuthException ex) when (ex.Code == code) { Console.WriteLine("PASS rejects " + code); return; }
 throw new Exception("Expected " + code);
}
static string Sid(AuthResponseDto auth) => new JwtSecurityTokenHandler().ReadJwtToken(auth.Token).Claims.Single(c => c.Type == "sid").Value;
try {
 await service.EnsureIndexesAsync();
 await service.Register(new SignUpDTO { Email = email.ToUpperInvariant(), Password = password });
 var user = await users.Find(u => u.Email == email).SingleAsync();
 Check(!user.IsEmailVerified && user.Sessions.Count == 0 && user.PasswordHash != password, "registration hashes password and requires verification");
 Check(user.Verification!.Hash != mail.Code && mail.Code.Length == 6, "stores only code hash");
 await Reject(async () => { await service.Login(new LoginDTO { Email = email, Password = password }); }, "email_not_verified");
 var code = mail.Code;
 await service.SendCode(email, false);
 Check(mail.Count == 1, "resend cooldown");
 var auth = await service.VerifyEmail(new VerifyEmailDTO { Email = email, Code = code });
 Check(await service.IsSessionActive(user.Id, Sid(auth)), "verification creates active session");
 await Reject(async () => { await service.VerifyEmail(new VerifyEmailDTO { Email = email, Code = code }); }, "invalid_code");
 var refreshed = await service.Refresh(auth.RefreshToken);
 Check(refreshed.RefreshToken != auth.RefreshToken, "refresh rotates");
 await Reject(async () => { await service.Refresh(auth.RefreshToken); }, "invalid_session");
 var login = await service.Login(new LoginDTO { Email = email, Password = password });
 await service.SendCode(email, true);
 var resetCode = mail.Code;
 await service.ResetPassword(new ResetPasswordDTO { Email = email, Code = resetCode, NewPassword = password + "new" });
 Check(!await service.IsSessionActive(user.Id, Sid(auth)), "reset invalidates existing JWT session");
 await Reject(async () => { await service.Refresh(refreshed.RefreshToken); }, "invalid_session");
 await Reject(async () => { await service.Refresh(login.RefreshToken); }, "invalid_session");
 await Reject(async () => { await service.Login(new LoginDTO { Email = email, Password = password }); }, "invalid_credentials");
 await Reject(async () => { await service.ResetPassword(new ResetPasswordDTO { Email = email, Code = resetCode, NewPassword = password }); }, "invalid_code");
 var fresh = await service.Login(new LoginDTO { Email = email, Password = password + "new" });
 await service.Logout(user.Id, Sid(fresh));
 Check(!await service.IsSessionActive(user.Id, Sid(fresh)), "logout revokes session");
 await service.SendCode(email, true);
 code = mail.Code;
 for (var i = 0; i < 5; i++) await Reject(async () => { await service.ResetPassword(new ResetPasswordDTO { Email = email, Code = code == "000000" ? "111111" : "000000", NewPassword = password }); }, "invalid_code");
 await Reject(async () => { await service.ResetPassword(new ResetPasswordDTO { Email = email, Code = code, NewPassword = password }); }, "invalid_code");
 await users.UpdateOneAsync(u => u.Email == email, Builders<User>.Update.Set("PasswordReset.SentAt", DateTime.UtcNow.AddMinutes(-11)));
 await service.SendCode(email, true);
 await users.UpdateOneAsync(u => u.Email == email, Builders<User>.Update.Set("PasswordReset.ExpiresAt", DateTime.UtcNow.AddSeconds(-1)));
 await Reject(async () => { await service.ResetPassword(new ResetPasswordDTO { Email = email, Code = mail.Code, NewPassword = password }); }, "invalid_code");
 mail.Fail = true;
 await Reject(async () => { await service.Register(new SignUpDTO { Email = "failure@example.com", Password = password }); }, "email_unavailable");
 var failed = await users.Find(u => u.Email == "failure@example.com").SingleAsync();
 Check(failed.Verification is null, "failed email can be retried immediately");
 Console.WriteLine("All authentication integration checks passed.");
} finally { await client.DropDatabaseAsync(name); }
class FakeMail : HttpMessageHandler {
 public string Code = ""; public int Count; public bool Fail;
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
  if (Fail) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
  var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
  Code = Regex.Match(payload.RootElement.GetProperty("text").GetString()!, @"\b\d{6}\b").Value;
  Count++;
  return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
 }
}
