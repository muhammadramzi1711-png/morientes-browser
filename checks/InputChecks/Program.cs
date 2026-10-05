using System;
using MorientesBrowser;

int passed=0;
void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);passed++;}
void Rejected(string value){try{BrowserInput.Normalize(value);throw new Exception("Unsafe input accepted: "+value);}catch(ArgumentException){passed++;Console.WriteLine("PASS: reject "+value.Split(':')[0]);}}
Check(BrowserInput.Normalize("google.com")=="https://google.com/","bare domain defaults to HTTPS");
Check(BrowserInput.Normalize("localhost:8080/test")=="http://localhost:8080/test","local development host");
Check(BrowserInput.Normalize("https://example.com/a?q=1#part")=="https://example.com/a?q=1#part","path, query, fragment survive");
Check(BrowserInput.Normalize("cara membuat browser")=="https://www.google.com/search?q=cara%20membuat%20browser","Indonesian search");
Check(BrowserInput.Normalize("a & b","duckduckgo")=="https://duckduckgo.com/?q=a%20%26%20b","search cannot inject extra query parameters");
Check(BrowserInput.Normalize("")==BrowserInput.HomeUrl,"empty address opens home");
Rejected("javascript:alert(document.cookie)");Rejected("data:text/html,<script>alert(1)</script>");Rejected("file:///C:/Windows/system.ini");Rejected("https://user:pass@evil.example");Rejected("https://example.com/\nmalicious");Rejected("https://example.com:99999/");
Check(BrowserInput.IsTrustedUi(BrowserInput.ChromeUrl),"chrome bridge origin accepted");
Check(BrowserInput.IsTrustedUi(BrowserInput.SidebarUrl),"sidebar bridge origin accepted");
Check(!BrowserInput.IsTrustedUi(BrowserInput.HomeUrl),"home does not get full bridge");
Check(!BrowserInput.IsTrustedUi(BrowserInput.ChromeUrl+"?spoof=1"),"unexpected UI query denied");
Check(!BrowserInput.IsTrustedUi("https://ui.morientes.invalid.evil.example/chrome.html"),"lookalike domain denied");
Check(!BrowserInput.IsTrustedUi("https://evil.example/"),"remote origin denied");
Console.WriteLine($"{passed} checks passed.");
