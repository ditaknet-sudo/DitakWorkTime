# TrueNAS SCALE & Docker Deployment Guide for Ditak WorkTime

Այս փաստաթուղթը ներկայացնում է **Ditak WorkTime** համակարգի տեղադրման, սպասարկման և թարմացման ուղեցույցը **TrueNAS SCALE**-ի և **Docker / Docker Compose**-ի համար։

---

## 1. Overview / Ակնարկ

Ditak WorkTime-ը նախագծված է որպես ինքնավար (single-tenant) Docker/TrueNAS հավելված (App), որը բաղկացած է հետևյալ սերվիսներից․

- **PostgreSQL 16** (`db`): Տվյալների բազա persistent ZFS/Docker Volume-ով
- **.NET 8 Web API** (`api`): Հիմնական տրամաբանություն և API
- **.NET 8 Background Worker** (`worker`): Հաշվարկների և ֆոնային առաջադրանքների մշակում
- **React Employee Web UI** (`web`): Աշխատակիցների ինտերֆեյս (Հայերեն, Անգլերեն, Ռուսերեն)
- **PHP Admin App** (`admin`): Ադմինիստրատիվ պանել
- **Nginx Reverse Proxy** (`proxy`): Միասնական մուտքային կետ (Port 80/Custom Port)

---

## 2. Installation on TrueNAS SCALE 24.10+ (Electric Eel)

TrueNAS SCALE 24.10 (Electric Eel) տարբերակից սկսած TrueNAS-ը նատիվ օգտագործում է **Docker** և **Docker Compose**։

### Քայլ 1: ZFS Dataset-ի ստեղծում (Persistent Storage)
1. TrueNAS Web UI-ում բացեք **Storage** -> **Datasets**։
2. Ստեղծեք նոր Dataset տվյալների բազայի համար (օրինակ` `/mnt/tank/apps/ditak-worktime/db_data`)։
3. Խորհուրդ է տրվում միացնել TrueNAS **Periodic Snapshot Tasks** այս dataset-ի համար` ավտոմատ պահուստավորման (backup) համար։

### Քայլ 2: Environment Variables (.env) Կարգավորում
Ստեղծեք `.env` ֆայլը կամ մուտքագրեք փոփոխականները TrueNAS App-ի մեջ․

```env
POSTGRES_DB=attendance
POSTGRES_USER=attendance
POSTGRES_PASSWORD=YourStrongDatabasePassword123!

JWT_SECRET=YourSuperSecretKeyWithMinimum32CharactersLength!
JWT_ISSUER=DitakWorkTime
JWT_AUDIENCE=DitakWorkTimeWeb
JWT_EXPIRES_MINUTES=480

COMPANY_NAME=YourCompany
COMPANY_TIMEZONE=Asia/Yerevan

SEED_ADMIN_EMAIL=admin@company.local
SEED_ADMIN_PASSWORD=ChangeMe123!
SEED_ADMIN_NAME=System Admin

CORS_ORIGINS=http://localhost,http://truenas.local
ADMIN_PUBLIC_URL=http://truenas.local/admin
```

### Քայլ 3: Custom App (Docker Compose) Ավելացում
1. Գնացեք **Apps** -> **Discover Apps** -> **Install Custom App** (կամ Compose)։
2. Տեղադրեք `docker-compose.yml` ֆայլի բովանդակությունը։
3. Volume binding հատվածում `db_data` volume-ը կարող եք կապել TrueNAS ZFS dataset path-ին․
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

## 3. Standard Docker & Docker Compose Installation

Ցանկացած Linux / Windows / macOS Docker host-ի վրա․

```bash
# 1. Պատճենել .env.example-ը
cp .env.example .env

# 2. Խմբագրել գաղտնաբառերը .env ֆայլում
nano .env

# 3. Գործարկել սերվերները
docker compose up -d --build
```

---

## 4. TrueNAS / Docker Access Points / Հասանելիություն

Գործարկումից հետո հավելվածը հասանելի է․

- **Employee Portal (React Web UI):** `http://<TRUENAS_IP>/`
- **Admin Panel:** `http://<TRUENAS_IP>/admin/`
- **API Health Check:** `http://<TRUENAS_IP>/health`
- **Swagger API Docs:** `http://<TRUENAS_IP>/api/`

---

## 5. Updates & Database Safety / Թարմացումներ և Տվյալների Անվտանգություն

1. **Տվյալների անվտանգություն:** Թարմացումների ժամանակ `db_data` ZFS dataset-ը / Docker Volume-ը **երբեք չի ջնջվում**։
2. **ZFS Snapshot (TrueNAS):** Թարմացնելուց առաջ TrueNAS Storage-ում կատարեք ZFS Snapshot։
3. **Update Script Executable:**
   ```bash
   sh deploy/scripts/update.sh
   ```
