# TrueNAS SCALE & Docker Deployment Guide for Ditak WorkTime

Այս փաստաթուղթը ներկայացնում է **Ditak WorkTime** համակարգի տեղադրման, սպասարկման և թարմացման ուղեցույցը **TrueNAS SCALE**-ի և **Docker / Docker Compose**-ի համար։

---

## 1. Overview / Ակնարկ

Ditak WorkTime-ը նախագծված է որպես ինքնավար (single-tenant) Docker/TrueNAS հավելված (App), որը բաղկացած է հետևյալ սերվիսներից․

- **PostgreSQL 16** (`db`): Տվյալների բազա persistent ZFS/Docker Volume-ով
- **.NET 8 Web API** (`api`): Հիմնական տրամաբանություն և API
- **.NET 8 Background Worker** (`worker`): Հաշվարկների, ֆոնային առաջադրանքների և Auto Check-Out-ի մշակում
- **React Employee Web UI** (`web`): Աշխատակիցների ինտերֆեյս (Հայերեն, Անգլերեն, Ռուսերեն)
- **PHP Admin App** (`admin`): Ադմինիստրատիվ + Տնօրեն + Հաշվապահ պանել
- **Nginx Reverse Proxy** (`proxy`): Միասնական մուտքային կետ — `HOST_PORT` (Default: **8888**)

> **⚠️ TrueNAS Port Ծանուցում**: TrueNAS SCALE-ն օգտագործում է 80-ը իր կառավարման UI-ի համար։ Ditak WorkTime-ն օգտագործում է **Port 8888** (կամ ձեր `.env`-ում սահմանած `HOST_PORT`)։

---

## 2. Roles & Access / Դերեր

| Դեր | Հասանելիություն |
|---|---|
| **Admin** | Ամեն ինչ — Employees, Sites, Users, Manual corrections, Reports |
| **Director (Տնօրեն)** | Live Presence board + Բոլոր report-ները (read-only) |
| **Accountant (Հաշվապահ)** | Monthly reports + Excel/PDF Export (read-only) |
| **Employee** | Անձնական check-in/out + Անձնական report history |

---

## 3. Installation on TrueNAS SCALE 24.10+ (Electric Eel)

TrueNAS SCALE 24.10 (Electric Eel) տարբերակից սկսած TrueNAS-ը նատիվ օգտագործում է **Docker** և **Docker Compose**։

### Քայլ 1: ZFS Dataset-ի ստեղծում (Persistent Storage)
1. TrueNAS Web UI-ում բացեք **Storage** -> **Datasets**։
2. Ստեղծեք Dataset (օրինակ` `/mnt/tank/apps/ditak-worktime/db_data`)։
3. Միացրեք **Periodic Snapshot Tasks** ավտոմատ backup-ի համար։

### Քայլ 2: Environment Variables (.env) Կարգավորում

```env
# TrueNAS Host Port — Ազատ port, TrueNAS 80-ը NOT
HOST_PORT=8888
HOST_BIND_IP=0.0.0.0

POSTGRES_DB=attendance
POSTGRES_USER=attendance
POSTGRES_PASSWORD=<UNIQUE_STRONG_DATABASE_PASSWORD>

JWT_SECRET=YourSuperSecretKeyWithMinimum32CharactersLength!
JWT_ISSUER=DitakWorkTime
JWT_AUDIENCE=DitakWorkTimeWeb
JWT_EXPIRES_MINUTES=480

COMPANY_NAME=YourCompany
COMPANY_TIMEZONE=Asia/Yerevan

SEED_ADMIN_EMAIL=admin@company.local
SEED_ADMIN_PASSWORD=<UNIQUE_ADMIN_PASSWORD_MIN_12_CHARS>
SEED_ADMIN_NAME=System Admin

CORS_ORIGINS=http://truenas.local:8888,http://localhost:8888
ADMIN_PUBLIC_URL=http://truenas.local:8888/admin

# Laravel session encryption key (`base64:` + 32 random bytes)
APP_KEY=<GENERATED_LARAVEL_APP_KEY>
```

### Քայլ 3: Custom App (Docker Compose) Ավելացում
1. Գնացեք **Apps** -> **Discover Apps** -> **Install Custom App** (կամ Compose)։
2. Տեղադրեք `docker-compose.yml` ֆայլի բովանդակությունը։
3. Volume binding-ի համար `db_data`-ն կապեք ZFS dataset path-ին․
   ```yaml
   volumes:
     db_data:
       driver: local
       driver_opts:
         type: none
         o: bind
         device: /mnt/tank/apps/ditak-worktime/db_data
   ```
4. Սեղմեք **Save / Deploy**։

---

## 4. Standard Docker & Docker Compose Installation

```bash
cp .env.example .env
nano .env   # Կարգավորեք HOST_PORT, գաղտնաբառեր, ժամային գոտի
docker compose up -d --build
```

---

## 5. TrueNAS / Docker Access Points / Հասանելիություն

Գործարկումից հետո (default `HOST_PORT=8888`)։

- **Employee Portal:** `http://<TRUENAS_IP>:8888/`
- **Admin Panel:** `http://<TRUENAS_IP>:8888/admin/`
- **API Health Check:** `http://<TRUENAS_IP>:8888/health`
- **Swagger API Docs:** `http://<TRUENAS_IP>:8888/swagger/`

---

## 6. Smart Features / Խելացի Գործառույթներ

- **Auto Check-Out**: Worker-ն ամեն կեսգիշերից հետո ինքնաբերաբար փակում է Open Shifts-ը
- **Network Presence**: 15 րոպե heartbeat window-ով live presence board
- **Export**: Excel (xlsx) + PDF per employee, per month

---

## 7. Updates & Database Safety / Թարմացումներ

1. `db_data` ZFS dataset-ը / Docker Volume-ը **երբեք չի ջնջվում** թարմացումների ժամանակ։
2. Թարմացնելուց առաջ TrueNAS Storage-ում կատարեք ZFS Snapshot։
3. Update script:
   ```bash
   sh deploy/scripts/update.sh
   ```
