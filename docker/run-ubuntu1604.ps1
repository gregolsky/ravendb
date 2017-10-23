param(
    $BindPort = 8080,
    $BindTcpPort = 38888,
    $ConfigPath = "",
    $DataDir = "",
    $PublicServerUrl = "",
    $PublicTcpServerUrl = "",
    $LogsMode = "",
    $CertificatePath = "",
    $CertificatePassword = "",
    $CertificatePasswordFile = "",
    $Hostname = "",
    [switch]$AuthenticationDisabled,
    [switch]$RemoveOnExit,
    [switch]$DryRun,
    [string]$Memory)

$ErrorActionPreference = "Stop";

function CheckLastExitCode {
    param ([int[]]$SuccessCodes = @(0), [scriptblock]$CleanupScript=$null)

    if ($SuccessCodes -notcontains $LastExitCode) {
        if ($CleanupScript) {
            "Executing cleanup script: $CleanupScript"
            &$CleanupScript
        }
        $msg = @"
EXE RETURNED EXIT CODE $LastExitCode
CALLSTACK:$(Get-PSCallStack | Out-String)
"@
        throw $msg
    }
}

$serverUrlScheme = "http"
if ([string]::IsNullOrEmpty($CertificatePath) -eq $false) {
    $serverUrlScheme = "https"
}

$dockerArgs = @('run')

# run in detached mode
$dockerArgs += '-d'

if ($RemoveOnExit) {
    $dockerArgs += '--rm'
}

if ($AuthenticationDisabled) {
    $dockerArgs += '-e'
    $dockerArgs += "UNSECURED_ACCESS_ALLOWED=PublicNetwork"
}

if ([string]::IsNullOrEmpty($DataDir) -eq $False) {
    write-host "Mounting $DataDir as RavenDB data dir."
    $dockerArgs += "-v"
    $dockerArgs += "`"$($DataDir):/databases`""
}

if ([string]::IsNullOrEmpty($ConfigPath) -eq $False) {
    if ($(Test-Path $ConfigPath) -eq $False) {
        throw "Config file does not exist under $ConfigPath path."
    }

    $configDir = Split-Path $ConfigPath 

    $containerConfigDir = "/opt/RavenDB/config"
    $containerConfigFile = Split-Path -Path $ConfigPath -Leaf
    $dockerArgs += "-v"
    $dockerArgs += "`"$($configDir):$containerconfigDir`""

    $dockerArgs += "-e"
    $envConfigPath = $containerConfigDir + '/' + $containerConfigFile 
    $dockerArgs += "`"CUSTOM_CONFIG_FILE=$envConfigPath`""

    write-host "Reading configuration from $ConfigPath"
}

if ([string]::IsNullOrEmpty($Memory) -eq $False) {
    $dockerArgs += "--memory=" + $Memory
    write-host "Memory limited to " + $memory
}

$machineName = [System.Environment]::MachineName

if ([string]::IsNullOrEmpty($PublicServerUrl)) {
    $PublicServerUrl = "$($serverUrlScheme)://$($machineName):$BindPort"   
}

if ([string]::IsNullOrEmpty($PublicTcpServerUrl)) {
    $PublicTcpServerUrl = "tcp://$($machineName):$BindTcpPort"   
}

if ([string]::IsNullOrEmpty($PublicServerUrl) -eq $False) {
    $dockerArgs += "-e" 
    $dockerArgs += "PUBLIC_SERVER_URL=$PublicServerUrl"
}

if ([string]::IsNullOrEmpty($PublicTcpServerUrl) -eq $False) {
    $dockerArgs += "-e" 
    $dockerArgs += "PUBLIC_TCP_SERVER_URL=$PublicTcpServerUrl"
}

if ([string]::IsNullOrEmpty($LogsMode) -eq $False) {
    $dockerArgs += "-e"
    $dockerArgs += "LOGS_MODE=$LogsMode"
}

if ([string]::IsNullOrEmpty($CertificatePath) -eq $False) {
    if ($(Test-Path $CertificatePath) -eq $False) {
        throw "Certificate file does not exist under $CertificatePath."
    }

    $containerCertDir = "/opt/RavenDB/cert"
    $containerCertFile = Split-Path -Leaf -Path $CertificatePath

    $hostDir = Split-Path $CertificatePath

    $dockerArgs += "-v"
    $dockerArgs += "`"$($hostDir):$containerCertDir`""

    $dockerArgs += "-e"
    $dockerArgs += "`"CERTIFICATE_PATH=$($containerCertDir + '/' + $containerCertFile)`""
}

if ([string]::IsNullOrEmpty($CertificatePassword) -eq $False) {
    $dockerArgs += "-e"
    $dockerArgs += "CERTIFICATE_PASSWORD=$CertificatePassword"
}

if ([string]::IsNullOrEmpty($CertificatePasswordFile) -eq $False) {
    if ($(Test-Path $CertificatePasswordFile) -eq $False) {
        throw "Certificate file does not exist under $CertificatePath."
    }

    $containerCertPassDir = "/opt/RavenDB/secrets"
    $containerCertPassFile = Split-Path -Leaf -Path $CertificatePasswordFile

    $hostDir = Split-Path $CertificatePasswordFile

    $dockerArgs += "-v"
    $dockerArgs += "`"$($hostDir):$containerCertPassDir`""

    $dockerArgs += "-e"
    $dockerArgs += "`"CERTIFICATE_PASSWORD_FILE=$($containerCertPassDir + '/' + $containerCertPassFile)`""
}

if ([string]::IsNullOrEmpty($Hostname) -eq $False) {
    $dockerArgs += "--hostname=$Hostname"
}

$dockerArgs += '-p'
$dockerArgs += "$($BindPort):8080"

$dockerArgs += '-p'
$dockerArgs += "$($BindTcpPort):38888"

$RAVEN_IMAGE = 'ravendb/ravendb:ubuntu-latest'
$dockerArgs += $RAVEN_IMAGE

if ($DryRun) {
    write-host -fore magenta "docker $dockerArgs"
    exit 0
}

write-host -nonewline "Starting container: "
write-host -fore magenta "docker $dockerArgs"

try {
    $containerId = Invoke-Expression -Command "docker $dockerArgs"
    CheckLastExitCode
} catch {
    write-host -ForegroundColor Red "Could not run docker image, please see error above for details."
    exit 1
}

$containerIdShort = $containerId.Substring(0, 10)

write-host -nonewline -fore white "***********************************************************"
write-host -fore red "
       _____                       _____  ____
      |  __ \                     |  __ \|  _ \
      | |__) |__ ___   _____ _ __ | |  | | |_) |
      |  _  // _` \  \ / / _ \ '_ \| |  | |  _ <
      | | \ \ (_| |\ V /  __/ | | | |__| | |_) |
      |_|  \_\__,_| \_/ \___|_| |_|_____/|____/
"
write-host -fore cyan "      Safe by default, optimized for efficiency"
write-host ""
write-host -nonewline "Container ID is "
write-host -fore white "$containerId"
write-host ""
write-host -nonewline "To stop it use:`t`t"
write-host -fore cyan "docker stop $containerIdShort"
write-host -nonewline "To run shell use:`t"
write-host -fore cyan "docker exec -it $containerIdShort /bin/bash"
write-host -nonewline "See output using:`t"
write-host -fore cyan "docker logs $containerIdShort"
write-host -nonewline "Inspect with:`t`t"
write-host -fore cyan "docker inspect $containerIdShort"

write-host ""
write-host -nonewline "Access RavenDB Studio on "
write-host -fore yellow "$PublicServerUrl"
write-host -nonewline "Listening for TCP connections on: "
write-host -fore yellow "$PublicTcpServerUrl"
write-host ""

write-host ""

write-host -fore white "***********************************************************"
