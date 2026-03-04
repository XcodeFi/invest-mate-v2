@echo off
echo ========================================
echo Investment Mate v2 - Authentication Test
echo ========================================

echo.
echo Checking prerequisites...

REM Check if backend is running
echo Testing backend health endpoint...
powershell -Command "try { $response = Invoke-WebRequest -Uri 'https://localhost:5001/health' -UseBasicParsing; Write-Host '✓ Backend is running (Status:' $response.StatusCode ')' } catch { Write-Host '✗ Backend not accessible:' $_.Exception.Message }"

echo.
echo Testing Google OAuth endpoints...

REM Test Google login endpoint (should redirect)
echo Testing Google login endpoint...
powershell -Command "try { $response = Invoke-WebRequest -Uri 'https://localhost:5001/api/auth/google/login' -UseBasicParsing -MaximumRedirection 0; Write-Host '✓ Google login endpoint accessible' } catch { if ($_.Exception.Response.StatusCode -eq 302) { Write-Host '✓ Google login endpoint working (redirect as expected)' } else { Write-Host '✗ Google login endpoint error:' $_.Exception.Message } }"

REM Test auth/me endpoint (should require auth)
echo Testing protected endpoint...
powershell -Command "try { $response = Invoke-WebRequest -Uri 'https://localhost:5001/api/auth/me' -UseBasicParsing; Write-Host '✗ Protected endpoint should require authentication' } catch { if ($_.Exception.Response.StatusCode -eq 401) { Write-Host '✓ Protected endpoint correctly requires authentication' } else { Write-Host '? Protected endpoint response:' $_.Exception.Response.StatusCode } }"

echo.
echo Checking frontend...

REM Check if frontend is running
echo Testing frontend...
powershell -Command "try { $response = Invoke-WebRequest -Uri 'http://localhost:4200' -UseBasicParsing; Write-Host '✓ Frontend is running' } catch { Write-Host '✗ Frontend not accessible:' $_.Exception.Message }"

echo.
echo Database check...

REM Check MongoDB connection
echo Testing MongoDB connection...
powershell -Command "try { $client = New-Object MongoDB.Driver.MongoClient('mongodb://localhost:27017'); $client.ListDatabases() | Out-Null; Write-Host '✓ MongoDB is accessible' } catch { Write-Host '✗ MongoDB not accessible:' $_.Exception.Message }"

echo.
echo ========================================
echo Test Summary:
echo - Backend should be running on https://localhost:5001
echo - Frontend should be running on http://localhost:4200
echo - MongoDB should be running on localhost:27017
echo - Google OAuth credentials should be configured
echo ========================================

echo.
echo Next steps:
echo 1. Configure Google OAuth credentials in appsettings.Development.json
echo 2. Start MongoDB: docker run -d --name mongodb -p 27017:27017 mongo:5.0
echo 3. Start backend: cd src/InvestmentApp.Api ^& dotnet run --launch-profile https
echo 4. Start frontend: cd frontend ^& npm start
echo 5. Test login at http://localhost:4200/auth/login

pause