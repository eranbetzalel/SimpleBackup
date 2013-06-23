Simple Backup
============

Simple Backup is a service that compresses configured user directories and upload the compressed backups to FTP server.

On a configured time of the day, the application will perform a single backup to each of the configured user directories. A backup run will either perform full or differential backup. Full backup will occur if none have been performed or the latest have was before the configured days between full backups, otherwise – differential backup will occur.

Configuration
---------
 * BackupPaths – comma delimited absolute path directories to backup. Example: C:\Test,C:\Test2.
 * ExcludedBackupPaths – comma delimited absolute path directories to exclude from backup. Example: C:\Test\excludeThis,C:\Test\andExcludeThis.
 * ExcludedFileTypes – comma delimited absolute path directories to backup. Example: exe,jpg,ini.
 * MinimumDaysBetweenFullBackups – the minimum amount of days before performing the next full backup.
 * FtpHost – the FTP server host or IP address.
 * FtpPort – the FTP server port.
 * FtpUserName – the FTP server username.
 * FtpPassword – the FTP server username's password.
 * RelativeFtpBackupDirectory – the relative directory backup path at the FTP server.
 * DailyBackupCompressStartTime - The daily time to start the backup. Example: 02:00:00.
 * DailyBackupStorageInterval - The period to wait before trying to upload any ready-to-upload backup (Seconds). Example: 300.

Installation
---------
 1. Compile (release).
 2. Copy the compiled code to any directory on your computer (for example: C:\Program Files (x86)\Simple Backup).
 3. Configure the application using the .config file.
 4. Install as service by running: Betzalel.SimpleBackup.exe install.

Console mode
---------
You can test your settings by running in console mode by running: Betzalel.SimpleBackup.exe -console.