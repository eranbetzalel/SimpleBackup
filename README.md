Simple Backup
============

Simple Backup is a console application that backup configured user directories and upload the backups to FTP server.

A single run of the application will perform a single backup to each of the configured user directories. A backup run will either perform full or differential backup. Full backup will occur if none have been performed or the latest have was before the configured days between full backups, otherwise – differential backup will occur.

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
