#include "CODENAME_TRACE.h"
#include "SpaceshipController.h"
#include "Engine.h"


// Sets default values
ASpaceshipController::ASpaceshipController()
{
	PrimaryActorTick.bCanEverTick = true;

	// Create static mesh component
	spaceshipMesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("SpaceshipMesh"));
	RootComponent = spaceshipMesh;

	// Create a spring arm component
	cameraToSpaceshipDistance = 15.f;
	springArm = CreateDefaultSubobject<USpringArmComponent>(TEXT("SpringArm"));
	springArm->SetupAttachment(RootComponent);
	springArm->TargetArmLength = 160.f;								// The camera follows at this distance behind the character	
	springArm->SocketOffset = FVector(0.f, 0.f, 60.f);
	springArm->bEnableCameraLag = true;
	springArm->CameraLagSpeed = cameraToSpaceshipDistance;
	springArm->bEnableCameraRotationLag = true;
	springArm->CameraRotationLagSpeed = 40.f;
	springArm->CameraLagMaxDistance = 150.f;
	springArm->bUsePawnControlRotation = false;

	// Create camera component 
	playerCamera = CreateDefaultSubobject<UCameraComponent>(TEXT("PlayerCamera"));
	playerCamera->SetupAttachment(springArm, USpringArmComponent::SocketName);
	playerCamera->bUsePawnControlRotation = false					// Don't rotate camera with controller

	AutoPossessPlayer = EAutoReceiveInput::Player0;

	//setup default variables
	maxSpeed = 5000.f;
	minSpeed = 450.f;
	maxPitchSpeed = 150.f;
	maxYawSpeed = 50.f;
	maxRollSpeed = 150.f;
	boostSpeed = 10000.0f;
	boostAcceleration = 1000.f;
	currentForwardSpeed = 0.0f;
	spaceshipVelocity = 0.f;
	rotationModifier = 1.f;
	currentBoostAcceleration = 0.f;
	currentXStrafeSpeed = 0.f;
	currentYStrafeSpeed = 0.f;
	maxXStrafeSpeed = 50.f;
	maxYStrafeSpeed = 50.f;

	bIsBoosting = false;			//the spaceship is not boosting by default

	//Add OnHit and ReceiveHit functionality
	spaceshipMesh->SetSimulatePhysics(true);
	spaceshipMesh->OnComponentHit.AddDynamic(this, &ASpaceshipController::OnHit);
}

// Called when the game starts or when spawned
void ASpaceshipController::BeginPlay()
{
	Super::BeginPlay();
}

// Called every frame
void ASpaceshipController::Tick( float DeltaTime )
{
	Super::Tick( DeltaTime );

	// the more the speed, the less the ability to rotate
	// If player ship is boosting, slow down the ability to turn
	if (bIsBoosting)
	{
		rotationModifier = 0.5f;
	}
	else
	{
		rotationModifier = FMath::Clamp(minSpeed / spaceshipVelocity, 0.8f, 1.0f);
	}
	
	FRotator deltaRotation(0, 0, 0);
	deltaRotation.Pitch = currentPitchSpeed*DeltaTime*rotationModifier;
	deltaRotation.Yaw = currentYawSpeed*DeltaTime*rotationModifier;
	deltaRotation.Roll = currentRollSpeed*DeltaTime*rotationModifier;

	//add yaw, pitch and roll
	AddActorLocalRotation(deltaRotation);

	//don't let the player change rotation if boosting

	/* --------------- Slowly speed up when player starts boost ------------------- */
	if (bIsBoosting)
	{
		float accelerationModifier = FMath::Clamp(minSpeed / currentForwardSpeed*2.5f, 0.01f, 2.5f);
		currentBoostAcceleration += DeltaTime*2.5f*currentBoostAcceleration;
		currentBoostSpeed = FMath::Clamp(currentBoostAcceleration, minSpeed, boostSpeed);
		currentForwardSpeed = currentBoostSpeed;
	}

	/* --------------- Slowly speed down when player releases the boost button ------------------- */

	else if (!bIsBoosting && spaceshipVelocity >= maxSpeed*1.5f)
	{
		currentBoostAcceleration -= DeltaTime*0.2f*currentBoostAcceleration;
		currentBoostSpeed = FMath::Clamp(currentBoostAcceleration, minSpeed, boostSpeed);
		currentForwardSpeed = currentBoostSpeed;
	}

	spaceshipVelocity = currentForwardSpeed;

	//DEBUG: Display velocity
	GEngine->AddOnScreenDebugMessage(-5, 0.5, FColor::Red, *(FString::SanitizeFloat(currentYStrafeSpeed)));

	//Add calculated velocity to the spaceship
	AddActorLocalOffset(FVector(spaceshipVelocity*DeltaTime, currentXStrafeSpeed, currentYStrafeSpeed));
}

// Called to bind functionality to input
void ASpaceshipController::SetupPlayerInputComponent(class UInputComponent* PlayerInputComponent)
{
	check(PlayerInputComponent);

	/* -------------- Setup movement input ---------------- */
	PlayerInputComponent->BindAxis("Thrust", this, &ASpaceshipController::ThrustInput);
	PlayerInputComponent->BindAxis("Pitch", this, &ASpaceshipController::PitchInput);
	PlayerInputComponent->BindAxis("Roll", this, &ASpaceshipController::RollInput);
	PlayerInputComponent->BindAxis("Yaw", this, &ASpaceshipController::YawInput);
	PlayerInputComponent->BindAxis("StrafeX", this, &ASpaceshipController::StrafeX);
	PlayerInputComponent->BindAxis("StrafeY", this, &ASpaceshipController::StrafeY);

	/* -------------- Setup boost input ---------------- */

	// only start boosting if player is not already boosting
	if (!bIsBoosting)
	{
		PlayerInputComponent->BindAction("Boost", IE_Pressed, this, &ASpaceshipController::StartBoost);
	}

	PlayerInputComponent->BindAction("Boost", IE_Released, this, &ASpaceshipController::StopBoost);
}

void ASpaceshipController::ThrustInput(float val)
{
	float currentAcceleration = val * currentForwardSpeed;

	// Calculate new speed
	float NewForwardSpeed = currentForwardSpeed + (GetWorld()->GetDeltaSeconds() * currentAcceleration);

	// Clamp between MinSpeed and MaxSpeed
	currentForwardSpeed = FMath::Clamp(NewForwardSpeed, minSpeed, maxSpeed);
}

void ASpaceshipController::PitchInput(float val)
{
	// Target pitch speed is based in input
	float targetPitchSpeed = FMath::Clamp(val * maxPitchSpeed * -1.f, -maxPitchSpeed, maxPitchSpeed);

	// When steering, we decrease pitch slightly
	targetPitchSpeed += (FMath::Abs(currentYawSpeed) * -0.2f);

	// Smoothly interpolate to target pitch speed
	currentPitchSpeed = FMath::FInterpTo(currentPitchSpeed, targetPitchSpeed, GetWorld()->GetDeltaSeconds(), 2.f);

}

void ASpaceshipController::YawInput(float val)
{
	float targetYawSpeed = FMath::Clamp(val*maxYawSpeed, -maxYawSpeed, maxYawSpeed);
	currentYawSpeed = FMath::FInterpTo(currentYawSpeed, targetYawSpeed, GetWorld()->GetDeltaSeconds(), 2.5f);
}

void ASpaceshipController::RollInput(float val)
{
	float targetRollSpeed = FMath::Clamp(val*maxRollSpeed, -maxRollSpeed, maxRollSpeed);
	currentRollSpeed = FMath::FInterpTo(currentRollSpeed, targetRollSpeed, GetWorld()->GetDeltaSeconds(), 2.5f);
}

void ASpaceshipController::StrafeX(float val)
{
	float targetXStrafeSpeed = FMath::Clamp(val*maxXStrafeSpeed, -maxXStrafeSpeed, maxXStrafeSpeed);
	currentXStrafeSpeed = FMath::FInterpTo(currentXStrafeSpeed, targetXStrafeSpeed, GetWorld()->GetDeltaSeconds(), 2.5f);
}

void ASpaceshipController::StrafeY(float val)
{
	float targetYStrafeSpeed = FMath::Clamp(val*maxYStrafeSpeed, -maxYStrafeSpeed, maxYStrafeSpeed);
	currentYStrafeSpeed = FMath::FInterpTo(currentYStrafeSpeed, targetYStrafeSpeed, GetWorld()->GetDeltaSeconds(), 2.5f);
}

void ASpaceshipController::StartBoost()
{
	bIsBoosting = true;
	currentBoostAcceleration = currentForwardSpeed;
	springArm->CameraLagSpeed = cameraToSpaceshipDistance * 0.5;
}

void ASpaceshipController::StopBoost()
{
	bIsBoosting = false;
	currentBoostAcceleration = currentBoostSpeed;
	springArm->CameraLagSpeed = FMath::Clamp(springArm->CameraLagSpeed, cameraToSpaceshipDistance*0.4f, cameraToSpaceshipDistance);
}

void ASpaceshipController::OnHit(UPrimitiveComponent* HitComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, FVector NormalImpulse, const FHitResult &Hit)
{
	FVector inverseForce = GetVelocity().GetSafeNormal();
	float forceValue = FMath::Clamp(currentForwardSpeed / boostSpeed*90.f, 20.f, 90.f);
	inverseForce = -1.f*forceValue * inverseForce;
	AddActorLocalOffset(inverseForce);
}