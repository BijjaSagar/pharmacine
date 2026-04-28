@echo off
set PGUSER=postgres
set PGPASSWORD=admin123
set PGDATABASE=PharmacyDB
set PGHOST=127.0.0.1
set PGPORT=5432

set BACKUP_DIR=D:\ClinicOS_Data\Backups
if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"

for /f "tokens=2-4 delims=/ " %%a in ('date /t') do (set mydate=%%c-%%a-%%b)
for /f "tokens=1-2 delims=/:" %%a in ('time /t') do (set mytime=%%a%%b)

set BACKUP_FILE=%BACKUP_DIR%\pharmacy_backup_%mydate%_%mytime%.sql

echo Backing up database to %BACKUP_FILE%...
pg_dump -h %PGHOST% -p %PGPORT% -U %PGUSER% -d %PGDATABASE% -F c -b -v -f "%BACKUP_FILE%"

echo Backup completed.
